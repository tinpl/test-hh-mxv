namespace Sample.Domain;

// todo: LocationBuilder
// todo: LocationServices
public class Location
{
  public string LocationName { get; init; }
  public DateTime LocationDate { get; init; }

  public Location(string locationName, DateTime locationDate)
  {
    LocationName = locationName;
    LocationDate = locationDate;
  }
}