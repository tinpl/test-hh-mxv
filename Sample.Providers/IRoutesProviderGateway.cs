using System.Collections.Immutable;
using Sample.Provider;

namespace Sample.Providers
{
  public interface IRoutesProviderGateway
  {
    Task<bool> Ping(CancellationToken cancellationToken);

    Task<ImmutableArray<ProviderRouteValueProposal>> Search(ProviderRoutesUpdateRequestedEvent ev,
      CancellationToken cancellationToken);
  }
}
