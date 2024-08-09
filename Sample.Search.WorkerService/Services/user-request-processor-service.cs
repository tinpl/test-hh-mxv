using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Options;
using Sample.Domain;
using Sample.Infrastructure;
using Sample.Provider;
using StackExchange.Redis;

namespace Sample.Search.WorkerService.Services
{
  // entry point for a user requests
  public class UserRequestProcessorService : BackgroundService
  {
    private readonly ILogger<UserRequestProcessorService> _logger;

    private readonly IConsumer<Ignore, RoutesSearchRequestPosted> _routesSearchRequestsPostedConsumer;
    private readonly IProducer<Null, ProviderRoutesUpdateRequestedEvent> _providerRoutesUpdateRequestedProducer;

    private readonly IProvidersInformationAccess _providersInformationAccess;
    private readonly IRedisClient _redis;
    private readonly IServiceProvider _serviceProvider;

    public UserRequestProcessorService(
      ILogger<UserRequestProcessorService> logger,
      IOptions<KafkaConfig> kafkaOptions,
      IProvidersInformationAccess providersInformationAccess,
      IRedisClient redis,
      IServiceProvider serviceProvider)
    {
      _logger = logger;
      _providersInformationAccess = providersInformationAccess;
      _redis = redis;
      _serviceProvider = serviceProvider;

      var schemaRegistryClient = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        { Url = kafkaOptions.Value.SchemaRegistryUrl });

      var consumerConfig = new ConsumerConfig
      {
        BootstrapServers = kafkaOptions.Value.BootstrapServers, 
        GroupId = nameof(UserRequestProcessorService),
        AllowAutoCreateTopics = true,
        EnableAutoCommit = true,
        EnableAutoOffsetStore = true,
        AutoOffsetReset = AutoOffsetReset.Earliest
      };
      _routesSearchRequestsPostedConsumer =
        new ConsumerBuilder<Ignore, RoutesSearchRequestPosted>(consumerConfig)
          .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
          .SetValueDeserializer(new JsonDeserializer<RoutesSearchRequestPosted>().AsSyncOverAsync())
          .Build();

      var producerConfig = new ProducerConfig
      {
        BootstrapServers = kafkaOptions.Value.BootstrapServers,
        AllowAutoCreateTopics = true,
      };

      _providerRoutesUpdateRequestedProducer =
        new ProducerBuilder<Null, ProviderRoutesUpdateRequestedEvent>(producerConfig)
          .SetValueSerializer(new JsonSerializer<ProviderRoutesUpdateRequestedEvent>(schemaRegistryClient))
          .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      try
      {
        IDatabase database = _redis.GetMuxer().GetDatabase();

        _routesSearchRequestsPostedConsumer.Subscribe("sample.events.user.routes-search.request-created");
        while (!stoppingToken.IsCancellationRequested)
        {
          ConsumeResult<Ignore, RoutesSearchRequestPosted> consumeResult;
          try
          {
            consumeResult = _routesSearchRequestsPostedConsumer.Consume(100);

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

          var routesSearchRequestPosted = consumeResult.Message.Value;

          // if not in cache, post search tasks
          if (!routesSearchRequestPosted.SearchRequest.Filters?.OnlyCached == true)
          {
            // create search request
            var providersIds = _providersInformationAccess.GetProviders()
              .Select(x => x.Id)
              .ToArray();

            // create search task as a redis set per request id:
            await database.SetAddAsync(
              $"response-{routesSearchRequestPosted.SearchRequestId}-routes-remaining-providers",
              providersIds.Select(x => new RedisValue(x)).ToArray());
            await database.KeyExpireAsync(
              $"response-{routesSearchRequestPosted.SearchRequestId}-routes-remaining-providers",
              TimeSpan.FromMinutes(5));

            await Task.WhenAll(
              providersIds.Select(providerId => _providerRoutesUpdateRequestedProducer.ProduceAsync(
                $"sample.events.provider-{providerId}.routes.update-requested",
                new Message<Null, ProviderRoutesUpdateRequestedEvent>
                {
                  Value = new ProviderRoutesUpdateRequestedEvent
                  {
                    ProviderId = providerId,
                    UserId = routesSearchRequestPosted.UserId,
                    SearchRequestId = routesSearchRequestPosted.SearchRequestId,
                    SearchRequest = routesSearchRequestPosted.SearchRequest
                  }
                }, stoppingToken)
              )
            );
          }
          // if search in 'cache' (let's call it a Model state)
          else
          {
            // search through the model
            ModelFacade modelFacade = new ModelFacade(_serviceProvider);

            ProviderRouteValueProposal[] valueProposals =
              await modelFacade.GetProposals().FindProposals(routesSearchRequestPosted, stoppingToken);

            await database.ListRightPushAsync($"response-{routesSearchRequestPosted.SearchRequestId}-routes",
              valueProposals.Select(proposal =>
                {
                  using var ms = new MemoryStream();
                  JsonSerializer.Serialize(ms, proposal);
                  return new RedisValue(Encoding.UTF8.GetString(ms.ToArray()));
                })
                .ToArray());

            var redisSubscriber = _redis.GetMuxer().GetSubscriber();
            await redisSubscriber
              .PublishAsync($"response-{routesSearchRequestPosted.SearchRequestId}-routes-update-status",
                $"data-received-from-provider-modelFacade");
            await redisSubscriber.PublishAsync(
              $"response-{routesSearchRequestPosted.SearchRequestId}-routes-update-status",
              "response-completed");
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex.ToString());
      }
    }

    public override void Dispose()
    {
      _routesSearchRequestsPostedConsumer.Close();
      _routesSearchRequestsPostedConsumer.Dispose();

      _providerRoutesUpdateRequestedProducer.Flush();
      _providerRoutesUpdateRequestedProducer.Dispose();

      base.Dispose();
    }
  }
}