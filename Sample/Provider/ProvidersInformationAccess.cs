namespace Sample.Provider
{
  public interface IRoutesProviderInformation
  {
    string Id { get; }
  }

  public class RoutesProviderInformation: IRoutesProviderInformation
  {
    public string Id { get; init; }
  }

  public interface IProvidersInformationAccess
  {
    IEnumerable<IRoutesProviderInformation> GetProviders();
    IRoutesProviderInformation GetProvider(string providerId);
  }

  public class ProviderNotFoundException : Exception
  {
    public readonly string ProviderId;

    public ProviderNotFoundException(string providerId) :
      base($"No suitable provider has been found with provider_id={providerId}")
    {
      ProviderId = providerId;
    }
  }

  // todo: move to configuration management or database
  public class ProvidersInformationAccess : IProvidersInformationAccess
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IRoutesProviderInformation> _providers = new ();

    public ProvidersInformationAccess(IServiceProvider serviceProvider)
    {
      _serviceProvider = serviceProvider;
    }

    void EnsureInitialized()
    {
      if (!_providers.Any())
      {
        _providers.Add(new RoutesProviderInformation { Id = "provider-one" });
        _providers.Add(new RoutesProviderInformation { Id = "provider-two" });
      }
    }

    public IEnumerable<IRoutesProviderInformation> GetProviders()
    {
      EnsureInitialized();
      return _providers;
    }

    public IRoutesProviderInformation GetProvider(string providerId)
    {
      EnsureInitialized();
      var ret = _providers.FirstOrDefault(x => x.Id == providerId);
      if (ret == null)
        throw new ProviderNotFoundException(providerId);

      return ret;
    }
  }
}