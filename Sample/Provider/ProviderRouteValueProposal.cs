using Sample.Domain;

namespace Sample.Provider;

public class ProviderRouteValueProposal
{
  public Guid Id { get; init; } = Guid.NewGuid();
  public DomainUser? ForUser { get; set; }
  public RouteSegment[] RouteSegments { get; set; }
  public decimal[] RouteSegmentsTravelPrices { get; set; }
  public DateTime ValidUntil { get; set; }
}