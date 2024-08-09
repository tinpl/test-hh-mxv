namespace Sample.Infrastructure
{
  public class ServerConfiguration
  {
    private static readonly string RuntimeId = $"{Environment.MachineName}_{Guid.NewGuid()}";

    public static string GetRuntimeId() { return RuntimeId; }
  }
}
