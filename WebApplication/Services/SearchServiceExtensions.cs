using Sample.Provider;

namespace WebApi.Services
{
  public static class SearchServiceExtensions
  {
    public static TestTask.Route ToApiRoute(this ProviderRouteValueProposal proposal)
    {
      return new TestTask.Route
      {
        Origin = proposal.RouteSegments.First().Origin.LocationName,
        OriginDateTime = proposal.RouteSegments.First().Origin.LocationDate,
        Destination = proposal.RouteSegments.Last().Destination.LocationName,
        DestinationDateTime = proposal.RouteSegments.Last().Destination.LocationDate,
        Price = proposal.RouteSegmentsTravelPrices.Sum(),
        TimeLimit = proposal.ValidUntil,
        Id = proposal.Id,
      };
    }
  }
}
