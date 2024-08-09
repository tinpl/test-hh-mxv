using TestTask;

namespace Sample.Search
{
  public class RoutesSearchRequestPosted
  {
    public string UserId { get; set; }
    public long SearchRequestId { get; set; }

    // todo: get rid of outbound API dependency here
    public SearchRequest SearchRequest { get; set; }
  }

  public class RoutesSearchRequestCompleted
  {
    public string UserId { get; set; }
    public long SearchRequestId { get; set; }
  }

  public class RoutesSearchTaskCreated
  {
    public long SearchTaskId { get; set; }
    public string[] ProvidersIdsRequested { get; set; }
  }

  public class RoutesSearchTaskDeleted
  {
    public long SearchTaskId { get; set; }
  }

  public class RoutesSearchTaskCompleted
  {
    public long SearchTaskId { get; set; }
  }


}
