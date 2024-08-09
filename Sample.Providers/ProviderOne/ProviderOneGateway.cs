using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Domain;
using Sample.Provider;
using TestTask;

namespace Sample.Providers.ProviderOne;

public class ProviderOneGateway: IRoutesProviderGateway
{
  private readonly HttpRoutesProviderClient _httpRoutesProviderClient;

  public ProviderOneGateway(IServiceProvider services)
  {
    _httpRoutesProviderClient = new HttpRoutesProviderClient(
      services.GetRequiredService<ILogger<HttpRoutesProviderClient>>(),
      services.GetRequiredService<IHttpClientFactory>(),
      new HttpRoutesProviderClientConfig { RequestUri = "http://provider-one/api/v1/" });
  }

  public Task<bool> Ping(CancellationToken cancellationToken)
  {
    return _httpRoutesProviderClient.Ping(cancellationToken);
  }

  public async Task<ImmutableArray<ProviderRouteValueProposal>> Search(ProviderRoutesUpdateRequestedEvent ev,
    CancellationToken cancellationToken)
  {
    var providerRequest = CreateRequest(ev);

    var providerResponse =
      await _httpRoutesProviderClient.Search<ProviderOneSearchRequest, ProviderOneSearchResponse>(
        providerRequest, cancellationToken);

    if (providerResponse == null)
      throw new Exception();

    var valueProposals = CreateValueProposals(providerResponse, ev);

    return valueProposals;
  }

  private static ProviderOneSearchRequest CreateRequest(ProviderRoutesUpdateRequestedEvent ev)
  {
    return new()
    {
      From = ev.SearchRequest.Origin,
      To = ev.SearchRequest.Destination,
      DateFrom = ev.SearchRequest.OriginDateTime,
      DateTo = ev.SearchRequest.Filters?.DestinationDateTime,
      MaxPrice = ev.SearchRequest.Filters?.MaxPrice
    };
  }

  private static ImmutableArray<ProviderRouteValueProposal> CreateValueProposals(
    ProviderOneSearchResponse providerResponse,
    ProviderRoutesUpdateRequestedEvent updateRequestedEvent)
  {
    return providerResponse.Routes.Select(responseRoute =>
      new ProviderRouteValueProposal
      {
        ForUser = new DomainUser { UserId = updateRequestedEvent.UserId },
        RouteSegments = new RouteSegment[]
        {
          new(new Location(responseRoute.From, responseRoute.DateFrom),
            new Location(responseRoute.To, responseRoute.DateTo)),
        },
        RouteSegmentsTravelPrices = new decimal[] { responseRoute.Price },
        ValidUntil = responseRoute.TimeLimit,
      }).ToImmutableArray();
  }

}