using Sample.Domain;
using Sample.Infrastructure;
using Sample.Provider;
using Sample.Search.WorkerService.Services;

namespace Sample.Search.WorkerService
{
  public class Program
  {
    public static void Main(string[] args)
    {
      IHost host = Host.CreateDefaultBuilder(args)
          .ConfigureServices((context, services ) =>
          {
            services.AddOptions<RedisConfig>()
              .Bind(context.Configuration.GetSection("Redis"));
            services.AddOptions<KafkaConfig>()
              .Bind(context.Configuration.GetSection("Kafka"));

            services.AddSingleton<IRedisClient, RedisClient>();
            services.AddTransient<IProvidersInformationAccess, ProvidersInformationAccess>();

            services.AddTransient<IProposalsProvider, CachedRouteProposalsProvider>();

            services.AddHostedService<UserRequestProcessorService>();
            services.AddHostedService<SearchResponseBuilderProviders>();
            services.AddHostedService<ModelBuilder>();
          })
          .Build();

      host.Run();
    }
  }
}