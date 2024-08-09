using Sample.Infrastructure;
using Sample.Providers.ProviderOne;
using Sample.Providers.ProviderTwo;
using Sample.Providers.Test;

namespace Sample.Providers.WorkerService
{
  public class Program
  {
    public static void Main(string[] args)
    {
      IHost host = Host.CreateDefaultBuilder(args)
          .ConfigureServices((context, services ) =>
          {
            services.AddOptions<KafkaConfig>()
              .Bind(context.Configuration.GetSection("Kafka"));

            services.AddHttpClient();

            if (context.HostingEnvironment.IsDevelopment())
            {
              services.AddKeyedTransient<IRoutesProviderGateway, ProviderTestGateway>("ProviderOneGateway");
              services.AddKeyedTransient<IRoutesProviderGateway, ProviderTestGateway>("ProviderTwoGateway");
            }
            else
            {
              services.AddKeyedTransient<IRoutesProviderGateway, ProviderOneGateway>("ProviderOneGateway");
              services.AddKeyedTransient<IRoutesProviderGateway, ProviderTwoGateway>("ProviderTwoGateway");
            }

            services.AddHostedService<ProviderOneConsumerService>();
            services.AddHostedService<ProviderTwoConsumerService>();
          })
          .Build();

      host.Run();
    }
  }
}