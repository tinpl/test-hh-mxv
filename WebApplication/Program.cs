using Sample.Infrastructure;
using Sample.Provider;
using WebApi.Services;

namespace WebApi
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Add services to the container.
      builder.Services.AddOptions<KafkaConfig>()
        .Bind(builder.Configuration.GetSection("Kafka"));

      builder.Services.AddOptions<RedisConfig>()
        .Bind(builder.Configuration.GetSection("Redis"));
      builder.Services.AddSingleton<IRedisClient, RedisClient>();

      builder.Services.AddTransient<IRoutesSearchService, RoutesSearchService>();
      builder.Services.AddTransient<IProvidersInformationAccess, ProvidersInformationAccess>();

      builder.Services.AddControllers();
      // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();

      builder.Services.AddHttpClient();

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
        app.UseSwagger();
        app.UseSwaggerUI();
      }

      app.UseHttpsRedirection();

      app.UseAuthorization();
      
      app.MapControllers();

      app.Run();
    }
  }
}