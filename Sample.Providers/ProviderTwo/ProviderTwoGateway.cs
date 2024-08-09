using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Domain;
using Sample.Provider;
using TestTask;

namespace Sample.Providers.ProviderTwo;

public class ProviderTwoGateway : IRoutesProviderGateway
{
  private readonly HttpRoutesProviderClient _httpRoutesProviderClient;

  public ProviderTwoGateway(IServiceProvider services)
  {
    _httpRoutesProviderClient = new HttpRoutesProviderClient(
      services.GetRequiredService<ILogger<HttpRoutesProviderClient>>(),
      services.GetRequiredService<IHttpClientFactory>(),
      new HttpRoutesProviderClientConfig { RequestUri = "http://provider-two/api/v1/" });
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
      await _httpRoutesProviderClient.Search<ProviderTwoSearchRequest, ProviderTwoSearchResponse>(
        providerRequest, cancellationToken);

    if (providerResponse == null)
      throw new Exception();

    var valueProposals = CreateValueProposals(providerResponse, ev);

    return valueProposals;
  }

  private static ProviderTwoSearchRequest CreateRequest(ProviderRoutesUpdateRequestedEvent ev)
  {
    return new()
    {
      Departure = ev.SearchRequest.Origin,
      Arrival = ev.SearchRequest.Destination,
      DepartureDate = ev.SearchRequest.OriginDateTime,
      MinTimeLimit = ev.SearchRequest.Filters?.MinTimeLimit
    };
  }

  private static ImmutableArray<ProviderRouteValueProposal> CreateValueProposals(
    ProviderTwoSearchResponse providerResponse,
    ProviderRoutesUpdateRequestedEvent updateRequestedEvent)
  {
    return providerResponse.Routes.Select(responseRoute =>
      new ProviderRouteValueProposal
      {
        ForUser = new DomainUser { UserId = updateRequestedEvent.UserId },
        RouteSegments = new RouteSegment[]
        {
          new(new Location(responseRoute.Departure.Point, responseRoute.Departure.Date),
            new Location(responseRoute.Arrival.Point, responseRoute.Arrival.Date)),
        },
        RouteSegmentsTravelPrices = new decimal[] { responseRoute.Price },
        ValidUntil = responseRoute.TimeLimit,
      }).ToImmutableArray();
  }

}