using System.Collections.Immutable;
using System.Linq;
using AutoFixture;
using Sample.Domain;
using Sample.Provider;

namespace Sample.Providers.Test
{
  public class ProviderTestGateway: IRoutesProviderGateway
  {
    public Task<bool> Ping(CancellationToken cancellationToken)
    {
      return Task.FromResult(true);
    }

    public async Task<ImmutableArray<ProviderRouteValueProposal>> Search(ProviderRoutesUpdateRequestedEvent ev,
      CancellationToken cancellationToken)
    {
      var fixture = new Fixture();
      var customization = new SupportMutableValueTypesCustomization();
      customization.Customize(fixture);

      var ret = await Task.FromResult(
        fixture.CreateMany<ProviderRouteValueProposal>()
        .Select(proposal =>
        {
          proposal.ForUser = new DomainUser { UserId = ev.UserId };

          var len = Math.Min(proposal.RouteSegments.Length, proposal.RouteSegmentsTravelPrices.Length);
          if (len < proposal.RouteSegments.Length)
            proposal.RouteSegments = proposal.RouteSegments[..len];
          if (len < proposal.RouteSegmentsTravelPrices.Length)
            proposal.RouteSegmentsTravelPrices = proposal.RouteSegmentsTravelPrices[..len];

          proposal.RouteSegments.First().Origin =
            new Location(ev.SearchRequest.Origin, ev.SearchRequest.OriginDateTime);
          proposal.RouteSegments.Last().Destination =
            new Location(ev.SearchRequest.Destination, 
              ev.SearchRequest.Filters?.DestinationDateTime ?? ev.SearchRequest.OriginDateTime + TimeSpan.FromDays(1));
          proposal.ValidUntil = DateTime.Now + TimeSpan.FromDays(3);

          return proposal;
        }));

      return ret.ToImmutableArray();
    }
  }
}
