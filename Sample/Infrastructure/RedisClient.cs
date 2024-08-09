using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Sample.Infrastructure
{
  public interface IRedisClient
  {
    ConnectionMultiplexer GetMuxer();
  }

  public class RedisClient: IRedisClient, IDisposable
  {
    private readonly ConnectionMultiplexer? _redis;

    public RedisClient(IOptions<RedisConfig> redisOptions)
    {
      _redis = ConnectionMultiplexer.Connect(redisOptions.Value.Host);
    }

    public ConnectionMultiplexer GetMuxer() => _redis!;

    public void Dispose()
    {
      _redis?.Dispose();
    }
  }
}
