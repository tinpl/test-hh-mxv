using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Options;
using Sample.Infrastructure;
using Sample.Provider;
using StackExchange.Redis;

namespace Sample.Search.WorkerService.Services
{
  // builds responses from provider updates
  public class SearchResponseBuilderProviders: BackgroundService
  {
    private readonly IConsumer<Ignore, ProviderRoutesUpdateReceivedEvent> _providerRoutesUpdateReceivedConsumer;
    private readonly IRedisClient _redis;
    private readonly ILogger<SearchResponseBuilderProviders> _logger;

    public SearchResponseBuilderProviders(
      ILogger<SearchResponseBuilderProviders> logger,
      IOptions<KafkaConfig> kafkaOptions, IRedisClient redisClient)
    {
      _logger = logger;
      _redis = redisClient;

      _providerRoutesUpdateReceivedConsumer =
        new ConsumerBuilder<Ignore, ProviderRoutesUpdateReceivedEvent>(new ConsumerConfig
          {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = nameof(SearchResponseBuilderProviders),
            AllowAutoCreateTopics = true,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = true,
            AutoOffsetReset = AutoOffsetReset.Earliest
          })
          .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
          .SetValueDeserializer(new JsonDeserializer<ProviderRoutesUpdateReceivedEvent>().AsSyncOverAsync())
          .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      try
      {
        IDatabase database = _redis.GetMuxer().GetDatabase();
        var redisSubscriber = _redis.GetMuxer().GetSubscriber();

        //_providerRoutesUpdateReceivedConsumer.Subscribe("^sample.events.provider-*.routes.update-received");
        _providerRoutesUpdateReceivedConsumer.Subscribe(new[] {
            "sample.events.provider-provider-one.routes.update-received",
            "sample.events.provider-provider-two.routes.update-received"
            });

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

          if (!await database.SetRemoveAsync(
                $"response-{providerRoutesUpdateReceived.SearchRequestId}-routes-remaining-providers",
                providerRoutesUpdateReceived.ProviderId))
          {
            // provider not enqueued in this task, or already processed, however we may receive data since we subscribed by regex, or may be reprocessing
            continue;
          }
          
          // add route proposals to (response-x-routes: list)
          await database.ListRightPushAsync($"response-{providerRoutesUpdateReceived.SearchRequestId}-routes",
            providerRoutesUpdateReceived.ValueProposals
              .Select(proposal =>
              {
                using var ms = new MemoryStream();
                JsonSerializer.Serialize(ms, proposal);
                return new RedisValue(Encoding.UTF8.GetString(ms.ToArray()));
              })
              .ToArray());
          await database.KeyExpireAsync($"response-{providerRoutesUpdateReceived.SearchRequestId}-routes",
            TimeSpan.FromMinutes(30));

          await redisSubscriber
            .PublishAsync($"response-{providerRoutesUpdateReceived.SearchRequestId}-routes-update-status",
              $"data-received-from-provider-{providerRoutesUpdateReceived.ProviderId}");

          // if this was the last expected provider
          if (0 == await database.SetLengthAsync(
                $"response-{providerRoutesUpdateReceived.SearchRequestId}-routes-remaining-providers"))
          {
            await redisSubscriber.PublishAsync(
              $"response-{providerRoutesUpdateReceived.SearchRequestId}-routes-update-status",
              "response-completed");
          }
        }
      }
      catch (Exception ex) { 
        _logger.LogError(ex.ToString());
      }
    }

    public override void Dispose()
    {
      _providerRoutesUpdateReceivedConsumer.Close();
      _providerRoutesUpdateReceivedConsumer.Dispose();
      base.Dispose();
    }
  }
}
