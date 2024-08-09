using TestTask;

namespace Sample.Provider
{
  public class ProviderRoutesUpdateRequestedEvent
  {
    public string ProviderId { get; init; }
    public long SearchRequestId { get; init; }

    public string UserId { get; init; }

    // todo: get rid of TestTask.SearchRequest dependency here
    public SearchRequest SearchRequest { get; init; }
  }

  public class ProviderRoutesUpdateFailedEvent
  {
    public string ProviderId { get; init; }
    public long SearchRequestId { get; init; }
  }

  public class ProviderRoutesUpdateReceivedEvent
  {
    public string ProviderId { get; init; }
    public long SearchRequestId { get; init; }

    // todo: Since these are not events, possibly idiomatically move from kafka (Distributed Event Log) to KeyValue store, correlate by UserId, SearchRequestId, ProviderId
    // todo: for kafka: should we use provider-typed routes here (serve as a request cache), or to convert to our domain model on fly (current)?
    public ProviderRouteValueProposal[] ValueProposals { get; init; }
  }
}
