namespace TestTask;

// HTTP POST http://provider-one/api/v1/search

public class ProviderOneSearchRequest
{
    // Mandatory
    // Start point of route, e.g. Moscow 
    public string From { get; set; }

    // Mandatory
    // End point of route, e.g. Sochi
    public string To { get; set; }

    // Mandatory
    // Start date of route
    public DateTime DateFrom { get; set; }

    // Optional
    // End date of route
    public DateTime? DateTo { get; set; }

    // Optional
    // Maximum price of route
    public decimal? MaxPrice { get; set; }
}

public class ProviderOneSearchResponse
{
    // Mandatory
    // Array of routes
    public ProviderOneRoute[] Routes { get; set; }
}

public class ProviderOneRoute
{
    // Mandatory
    // Start point of route
    public string From { get; set; }

    // Mandatory
    // End point of route
    public string To { get; set; }

    // Mandatory
    // Start date of route
    public DateTime DateFrom { get; set; }

    // Mandatory
    // End date of route
    public DateTime DateTo { get; set; }

    // Mandatory
    // Price of route
    public decimal Price { get; set; }

    // Mandatory
    // Timelimit. After it expires, route became not actual
    public DateTime TimeLimit { get; set; }
}

// HTTP GET http://provider-one/api/v1/ping
//      - HTTP 200 if provider is ready
//      - HTTP 500 if provider is down