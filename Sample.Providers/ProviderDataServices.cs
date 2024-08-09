using System.Collections.Immutable;
using Sample.Provider;

namespace Sample.Providers;

public static class ProviderDataServices
{
  public static ImmutableArray<ProviderRouteValueProposal> FilterData(
    ImmutableArray<ProviderRouteValueProposal> valueProposals,
    ProviderRoutesUpdateRequestedEvent ev)
  {
    /*
       e.g.

      if (ev.SearchRequest.Filters is { MaxPrice: not null } &&
          responseRoute.Price > ev.SearchRequest.Filters.MaxPrice)
      {
        // log provider error
        continue;
      }
      */

    return valueProposals.Where(proposal =>
      {
        if (ev.SearchRequest.Filters is { MinTimeLimit: not null } &&
            proposal.ValidUntil < ev.SearchRequest.Filters.MinTimeLimit)
        {
          return false;
        }

        if (ev.SearchRequest.Filters is { MaxPrice: not null } &&
            proposal.RouteSegmentsTravelPrices.Sum() > ev.SearchRequest.Filters.MaxPrice)
        {
          return false;
        }

        return true;
      })
      .ToImmutableArray();
  }

}