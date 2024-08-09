// and here may be Java with Kafka Streams or even Flink, but it is out of a scope now
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Sample.Provider;
using Microsoft.Extensions.Options;
using Sample.Domain;
using Sample.Infrastructure;

namespace Sample.Search.WorkerService.Services
{
  // builds Domain model state from provider updates (cache)
  public class ModelBuilder : BackgroundService
  {
    private readonly IConsumer<Ignore, ProviderRoutesUpdateReceivedEvent> _providerRoutesUpdateReceivedConsumer;
    private readonly ILogger<ModelBuilder> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ModelBuilder(ILogger<ModelBuilder> logger, IOptions<KafkaConfig> kafkaOptions, 
      IServiceProvider serviceProvider)
    {
      _logger = logger;
      _serviceProvider = serviceProvider;

      var consumerConfig = new ConsumerConfig
      {
        BootstrapServers = kafkaOptions.Value.BootstrapServers,
        GroupId = nameof(ModelBuilder),
        AllowAutoCreateTopics = true,
        EnableAutoCommit = true,
        EnableAutoOffsetStore = true,
        AutoOffsetReset = AutoOffsetReset.Earliest
      };

      _providerRoutesUpdateReceivedConsumer =
        new ConsumerBuilder<Ignore, ProviderRoutesUpdateReceivedEvent>(consumerConfig)
          .SetValueDeserializer(new JsonDeserializer<ProviderRoutesUpdateReceivedEvent>().AsSyncOverAsync())
          .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      try
      {
        _providerRoutesUpdateReceivedConsumer.Subscribe(new[] {
          "sample.events.provider-provider-one.routes.update-received",
          "sample.events.provider-provider-two.routes.update-received"
        });

        var proposals = new ModelFacade(_serviceProvider).GetProposals();

        while (!stoppingToken.IsCancellationRequested)
        {
          ConsumeResult<Ignore, ProviderRoutesUpdateReceivedEvent> consumeResult;
          try
          {
            consumeResult = _providerRoutesUpdateReceivedConsumer.Consume(100);

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
            _logger.LogError(ex.ToString());
            continue;
          }

          var providerRoutesUpdateReceived = consumeResult.Message.Value;
          
          await proposals.AddProposals(providerRoutesUpdateReceived.ProviderId,
            providerRoutesUpdateReceived.ValueProposals, stoppingToken);
        }
      }
      finally
      {
        _providerRoutesUpdateReceivedConsumer.Close();
      }
    }

    public override void Dispose()
    {
      _providerRoutesUpdateReceivedConsumer.Dispose();
      base.Dispose();
    }
  }
}
