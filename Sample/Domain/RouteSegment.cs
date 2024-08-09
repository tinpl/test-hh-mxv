namespace Sample.Domain;

public class RouteSegment
{
  public Location Origin { get; set; }
  public Location Destination { get; set; }

  public RouteSegment(Location origin, Location destination)
  {
    Origin = origin;
    Destination = destination;
  }
}