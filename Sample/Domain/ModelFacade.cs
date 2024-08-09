using Microsoft.Extensions.DependencyInjection;

namespace Sample.Domain;
/*
- assume we don't want to use Integration Framework like Apache Camel, or Spring Integration here
*/

// Given the whole domain of locations, with capability to be assembled into routes
// have solutions of UserRequests by Business proposals in form of ProviderRoutes

// ModelFacade: Name-time, Locations, Named locations (time dependent), single-dimensional one-directional time
// User acts to Find proposals as a requests to HTTP API, so it's outside of scope (User.MakeSearchRequest)
public class ModelFacade
{
  private readonly IServiceProvider _serviceProvider;

  public ModelFacade(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
  }
  
  public IProposalsProvider GetProposals()
  {
    return _serviceProvider.GetRequiredService<IProposalsProvider>();
  }
}