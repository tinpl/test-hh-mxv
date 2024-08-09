using System.Text;
using System.Text.Json;
using TestTask;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.Extensions.Options;
using Sample.Infrastructure;
using Sample.Search;
using StackExchange.Redis;
using Sample.Provider;

namespace WebApi.Services
{
  public interface IRoutesSearchService
  {
    Task<SearchResponse> SearchAsync(string userId, long searchRequestId, SearchRequest request, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(string userId, CancellationToken cancellationToken);
  }

  // Since we can possibly do serverless deploy here (it is not uncommon for an API layer),
  // do not use any long-running services in from-controller accessible services (e.g. kafka consumers)
  // Service posts requests to kafka, gets response from redis, gets notification from redis pub-sub
  public class RoutesSearchService : IRoutesSearchService, IDisposable
  {
    private readonly IProducer<Null, RoutesSearchRequestPosted> _routesSearchRequestCreatedProducer;
    private readonly IRedisClient _redis;

    public RoutesSearchService(IOptions<KafkaConfig> kafkaOptions, IRedisClient redis)
    {
      _redis = redis;

      var producerConfig = new ProducerConfig
      {
        BootstrapServers = kafkaOptions.Value.BootstrapServers,
        AllowAutoCreateTopics = true
      };

      var schemaRegistryClient = new CachedSchemaRegistryClient(new SchemaRegistryConfig
        { Url = kafkaOptions.Value.SchemaRegistryUrl });

      _routesSearchRequestCreatedProducer = new ProducerBuilder<Null, RoutesSearchRequestPosted>(producerConfig)
        .SetValueSerializer(new JsonSerializer<RoutesSearchRequestPosted>(schemaRegistryClient))
        .Build();
    }

    public async Task<SearchResponse> SearchAsync(string userId, long searchRequestId, SearchRequest request,
      CancellationToken cancellationToken)
    {
      IDatabase database = _redis.GetMuxer().GetDatabase();
      
      var subscriber = _redis.GetMuxer().GetSubscriber();
      var subscriptionChannel = RedisChannel.Literal($"response-{searchRequestId}-routes-update-status");
      ChannelMessageQueue responseMessagesQueue = await subscriber.SubscribeAsync(subscriptionChannel);

      await _routesSearchRequestCreatedProducer.ProduceAsync(
        "sample.events.user.routes-search.request-created",
        new Message<Null, RoutesSearchRequestPosted>
        {
          Value = new RoutesSearchRequestPosted
          {
            UserId = userId,
            SearchRequestId = searchRequestId,
            SearchRequest = request,
          }
        },
        cancellationToken);

      // wait for completion message or timeout
      var tcs = new TaskCompletionSource();

      var fireAndForget = Task.Run(async () =>
      {
        await foreach (ChannelMessage message in responseMessagesQueue)
        {
          if (cancellationToken.IsCancellationRequested)
          {
            tcs.SetCanceled(cancellationToken);
            break;
          }

          if (message.Message == "response-completed")
          {
            tcs.SetResult();
            break;
          }

          // do something with provider message, e.g. data-received-from-provider-{provider-id}
          if (message.Message.StartsWith("data-received-from-provider-"))
          {
            var providerId = message.Message.ToString().Substring("data-received-from-provider-".Length);
          }
        }
      }, cancellationToken).ConfigureAwait(false);

      await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(5), cancellationToken), tcs.Task);
      await subscriber.UnsubscribeAsync(subscriptionChannel);

      // read response being built in (routes-search-response-builder)
      var routes = (await database.ListRangeAsync($"response-{searchRequestId}-routes"))
        .Select(x =>
        {
          byte[] byteArray = Encoding.UTF8.GetBytes(x.ToString());
          using MemoryStream stream = new MemoryStream(byteArray);
          var proposal = JsonSerializer.Deserialize<ProviderRouteValueProposal>(stream);
          return proposal!.ToApiRoute();
        })
        .ToArray();

      if (!routes.Any())
      {
        return new SearchResponse
        {
          Routes = routes,
          MaxPrice = 0,
          MinPrice = 0,
          MinMinutesRoute = 0,
          MaxMinutesRoute = 0
        };
      }

      var minTimeRoute = routes.MinBy(x => x.DestinationDateTime - x.OriginDateTime);
      var maxTimeRoute = routes.MaxBy(x => x.DestinationDateTime - x.OriginDateTime);

      var ret = new SearchResponse
      {
        Routes = routes,
        MaxPrice = routes.MaxBy(x => x.Price)!.Price,
        MinPrice = routes.MinBy(x => x.Price)!.Price,
        MinMinutesRoute = Convert.ToInt32((minTimeRoute!.DestinationDateTime - minTimeRoute!.OriginDateTime).TotalMinutes),
        MaxMinutesRoute = Convert.ToInt32((maxTimeRoute!.DestinationDateTime - maxTimeRoute!.OriginDateTime).TotalMinutes)
      };

      return ret;
    }

    public Task<bool> IsAvailableAsync(string userId, CancellationToken cancellationToken)
    {
      if (string.IsNullOrEmpty(userId))
        return Task.FromResult(false);

      if (!_redis.GetMuxer().IsConnected)
        return Task.FromResult(false);

      return Task.FromResult(true);
    }

    public void Dispose()
    {
      _routesSearchRequestCreatedProducer.Flush();
      _routesSearchRequestCreatedProducer.Dispose();
    }
  }
}