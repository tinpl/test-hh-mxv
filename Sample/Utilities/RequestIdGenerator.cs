namespace Sample.Utilities {

// todo: do not use this, generate outside on LB or distributed ID generator
  public static class RequestIdGenerator
  {
    private static readonly Random Random = new();
    public static long Next()
    {
      return Random.Next(); ;
    }
  }
}