namespace TestTask;

// HTTP POST http://provider-two/api/v1/search
public class ProviderTwoSearchRequest
{
    // Mandatory
    // Start point of route, e.g. Moscow 
    public string Departure { get; set; }

    // Mandatory
    // End point of route, e.g. Sochi
    public string Arrival { get; set; }

    // Mandatory
    // Start date of route
    public DateTime DepartureDate { get; set; }

    // Optional
    // Minimum value of timelimit for route
    public DateTime? MinTimeLimit { get; set; }
}

public class ProviderTwoSearchResponse
{
    // Mandatory
    // Array of routes
    public ProviderTwoRoute[] Routes { get; set; }
}

public class ProviderTwoRoute
{
    // Mandatory
    // Start point of route
    public ProviderTwoPoint Departure { get; set; }


    // Mandatory
    // End point of route
    public ProviderTwoPoint Arrival { get; set; }

    // Mandatory
    // Price of route
    public decimal Price { get; set; }

    // Mandatory
    // Timelimit. After it expires, route became not actual
    public DateTime TimeLimit { get; set; }
}

public class ProviderTwoPoint
{
    // Mandatory
    // Name of point, e.g. Moscow\Sochi
    public string Point { get; set; }

    // Mandatory
    // Date for point in Route, e.g. Point = Moscow, Date = 2023-01-01 15-00-00
    public DateTime Date { get; set; }
}

// HTTP GET http://provider-two/api/v1/ping
//      - HTTP 200 if provider is ready
//      - HTTP 500 if provider is down