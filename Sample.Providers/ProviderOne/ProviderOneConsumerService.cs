using System.Collections.Immutable;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sample.Infrastructure;
using Sample.Provider;

namespace Sample.Providers.ProviderOne
{
  public class ProviderOneConsumerService : BackgroundService
  {
    const int kafka_consume_exception_id = 502;
    const int unhandled_exception_id = 500;

    private const string ProviderId = "provider-one";

    private readonly ILogger<ProviderOneConsumerService> _logger;
    private readonly IServiceProvider _services;

    private readonly KafkaConfig _kafkaConfig;
    private readonly CachedSchemaRegistryClient _schemaRegistryClient;
    private readonly IConsumer<Ignore, ProviderRoutesUpdateRequestedEvent> _providerRoutesUpdateRequestedConsumer;
    private readonly IProducer<Null, ProviderRoutesUpdateReceivedEvent> _providerRoutesUpdateReceivedProducer;

    public ProviderOneConsumerService(IOptions<KafkaConfig> kafkaOptions,
      ILogger<ProviderOneConsumerService> logger, IServiceProvider services)
    {
      _kafkaConfig = kafkaOptions.Value;
      _logger = logger;
      _services = services;

      _schemaRegistryClient = new CachedSchemaRegistryClient(new SchemaRegistryConfig
      { Url = kafkaOptions.Value.SchemaRegistryUrl });

      var consumerConfig = new ConsumerConfig
      {
        BootstrapServers = _kafkaConfig.BootstrapServers,
        GroupId = nameof(Providers),
        AllowAutoCreateTopics = true, // https://github.com/confluentinc/confluent-kafka-go/issues/
        EnableAutoCommit = true,
        EnableAutoOffsetStore = true,
        AutoOffsetReset = AutoOffsetReset.Earliest
      };
      _providerRoutesUpdateRequestedConsumer =
        new ConsumerBuilder<Ignore, ProviderRoutesUpdateRequestedEvent>(consumerConfig)
          .SetValueDeserializer(new JsonDeserializer<ProviderRoutesUpdateRequestedEvent>().AsSyncOverAsync())
          .Build();

      _providerRoutesUpdateReceivedProducer = new ProducerBuilder<Null, ProviderRoutesUpdateReceivedEvent>(
          new ProducerConfig
          {
            BootstrapServers = _kafkaConfig.BootstrapServers,
            AllowAutoCreateTopics = true
          })
        .SetValueSerializer(new JsonSerializer<ProviderRoutesUpdateReceivedEvent>(_schemaRegistryClient))
        .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      try
      {
        _providerRoutesUpdateRequestedConsumer.Subscribe(
          $"sample.events.provider-{ProviderId}.routes.update-requested");

        var gateway = _services.GetRequiredKeyedService<IRoutesProviderGateway>("ProviderOneGateway");

        while (!stoppingToken.IsCancellationRequested)
        {
          ConsumeResult<Ignore, ProviderRoutesUpdateRequestedEvent> consumeResult;
          try
          {
            consumeResult = _providerRoutesUpdateRequestedConsumer.Consume(100);

            if (consumeResult == null)
              continue;

            if (consumeResult.IsPartitionEOF)
            {
              _logger.LogInformation(
                $"Reached end of topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}.");
              continue;
            }
          }
          catch (ConsumeException ex)
          {
            _logger.LogError(new EventId(kafka_consume_exception_id), ex, ex.ToString());
            continue;
          }

          ProviderRoutesUpdateRequestedEvent providerRoutesUpdateRequested = consumeResult.Message.Value;

          while (!await gateway.Ping(stoppingToken)) // infinite wait
          {
            // todo: whole method of doing this in code seems to be suboptimal, maybe integrating some Service Discovery solution (e.g. Consul) with service availability based on HTTP checks will work more concise
            await Task.Delay(1000, stoppingToken);
            _logger.LogInformation("httpRoutesProviderClient.Ping == false");
          }

          ImmutableArray<ProviderRouteValueProposal> valueProposals;
          try
          {
            valueProposals = await gateway.Search(providerRoutesUpdateRequested, stoppingToken);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex.ToString());

            using var producer = new ProducerBuilder<Null, ProviderRoutesUpdateFailedEvent>(new ProducerConfig
            {
              BootstrapServers = _kafkaConfig.BootstrapServers,
              AllowAutoCreateTopics = true
            })
              .SetValueSerializer(new JsonSerializer<ProviderRoutesUpdateFailedEvent>(_schemaRegistryClient))
              .Build();

            await producer.ProduceAsync($"sample.events.provider-{ProviderId}.routes.update-failed",
              new Message<Null, ProviderRoutesUpdateFailedEvent>
              {
                Value = new ProviderRoutesUpdateFailedEvent
                {
                  ProviderId = ProviderId,
                  SearchRequestId = providerRoutesUpdateRequested.SearchRequestId
                }
              }, stoppingToken);

            continue;
          }

          // todo: do we trust providers? double check here for all returned data
          valueProposals = ProviderDataServices.FilterData(valueProposals, providerRoutesUpdateRequested);
          
          await _providerRoutesUpdateReceivedProducer.ProduceAsync(
            $"sample.events.provider-{ProviderId}.routes.update-received",
            new Message<Null, ProviderRoutesUpdateReceivedEvent>
            {
              Value = new ProviderRoutesUpdateReceivedEvent
              {
                ProviderId = ProviderId,
                SearchRequestId = providerRoutesUpdateRequested.SearchRequestId,
                ValueProposals = valueProposals.ToArray()
              }
            }, stoppingToken);

          // _providerRoutesUpdateRequestedConsumer.Commit(consumeResult);
        }
      }
      catch (Exception e)
      {
        _logger.LogError(new EventId(unhandled_exception_id), e, "Unhandled exception");
      }
      finally
      {
        _providerRoutesUpdateRequestedConsumer.Close();
      }
    }

    private static ResiliencePipeline CreateResiliencePipeline()
    {
      // todo: add HttpProviderAccessConfig

      return new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 10 })
        .AddTimeout(TimeSpan.FromSeconds(10))
        .Build();
    }

    public override void Dispose()
    {
      _providerRoutesUpdateRequestedConsumer.Dispose();

      _providerRoutesUpdateReceivedProducer.Flush();
      _providerRoutesUpdateReceivedProducer.Dispose();

      base.Dispose();
    }
  }
}