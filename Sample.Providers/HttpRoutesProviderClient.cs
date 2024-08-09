using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sample.Providers
{
  public class HttpRoutesProviderClientConfig
  {
    public string RequestUri { get; init; }
  }

  // todo: probably will be better to unwrap a copy of this into each usage
  public class HttpRoutesProviderClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly HttpRoutesProviderClientConfig _config;

    public HttpRoutesProviderClient(ILogger<HttpRoutesProviderClient> logger,
      IHttpClientFactory httpClientFactory, HttpRoutesProviderClientConfig config)
    {
      _httpClient = httpClientFactory.CreateClient();
      _logger = logger;
      _config = config;
    }

    public async Task<bool> Ping(CancellationToken cancellationToken)
    {
      var response = await _httpClient.GetAsync(_config.RequestUri + "ping", cancellationToken);
      switch (response.StatusCode)
      {
        case HttpStatusCode.OK:
          return true;
        case HttpStatusCode.InternalServerError:
          return false;
        default:
          _logger.LogError(
            "Provider.Ping received an unacceptable Status code={StatusCode}",
            response.StatusCode);
          return false;
      }
    }

    public async Task<TResponseType?> Search<TRequestType, TResponseType>(
      TRequestType providerRequest, CancellationToken cancellationToken)
    {
      var response = await _httpClient.PostAsJsonAsync(_config.RequestUri + "search",
        providerRequest,
        cancellationToken);

      TResponseType? providerResponse =
        await response.Content.ReadFromJsonAsync<TResponseType>(
          new JsonSerializerOptions(JsonSerializerDefaults.Web),
          cancellationToken);

      return providerResponse!;
    }

  }
}