using Sample.Provider;
using Sample.Search;

namespace Sample.Domain;

public interface IProposalsProvider
{
  Task<ProviderRouteValueProposal[]> FindProposals(RoutesSearchRequestPosted request,
    CancellationToken cancellationToken);

  Task AddProposals(string providerId, ProviderRouteValueProposal[] valueProposals,
    CancellationToken cancellationToken);
}