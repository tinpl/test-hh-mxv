using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using Sample.Infrastructure;
using Sample.Provider;
using Sample.Search;
using StackExchange.Redis;

namespace Sample.Domain;

public class CachedRouteProposalsProvider : IProposalsProvider
{
  private readonly ILogger<CachedRouteProposalsProvider> _logger;
  private readonly IDatabase _database;

  public CachedRouteProposalsProvider(ILogger<CachedRouteProposalsProvider> logger,
    IRedisClient redisClient)
  {
    _logger = logger;
    _database = redisClient.GetMuxer().GetDatabase();
  }

  public async Task<ProviderRouteValueProposal[]> FindProposals(RoutesSearchRequestPosted request,
    CancellationToken cancellationToken)
  {
    if (!request.SearchRequest.Filters!.OnlyCached == true)
      throw new ArgumentException("request should contain OnlyCached == true");

    // honestly, some SQL query by indexes would be just ok there,
    // probably should go with redis-stack or postgresql here due to simplicity.
    // as search logic expands and overpasses regular SQL queries consider ElasticSearch

    var ft = _database.FT();
    
    await CreateProposalsIndexIfNotExists(ft);

    var sr = request.SearchRequest;

    StringBuilder sb = new StringBuilder();
    sb.Append($"@origin:({sr.Origin})");
    sb.Append($" @origin_date_time:{sr.OriginDateTime.Ticks}");
    sb.Append($" @destination:({sr.Destination})");
    if (sr.Filters != null)
    {
      if (sr.Filters.DestinationDateTime != null)
        sb.Append($" @destination_date_time:{sr.Filters.DestinationDateTime.Value.Ticks}");
      if (sr.Filters.MaxPrice != null)
        sb.Append($" @price:[0, {sr.Filters.MaxPrice}]");
      if (sr.Filters.MinTimeLimit != null)
        sb.Append($" @valid_until:[{sr.Filters.MinTimeLimit.Value.Ticks}, {long.MaxValue}]");
    }

    var searchQuery = sb.ToString();
    
    var searchResult = await ft.SearchAsync("idx:proposals", new Query(searchQuery));
    
    var ret = new ConcurrentBag<ProviderRouteValueProposal>();

    var tasks = searchResult.Documents.Select(async document =>
    {
      //proposal:{id}
      //string guid = document.Id.Split(":")[1];
      //var proposalString = await _database.HashGetAsync($"proposal:{guid}", "proposal");

      var proposalString = document.GetProperties().FirstOrDefault(x => x.Key == "proposal").Value;

      byte[] byteArray = Encoding.UTF8.GetBytes(proposalString.ToString());
      using MemoryStream stream = new MemoryStream(byteArray);

      var add = await JsonSerializer.DeserializeAsync<ProviderRouteValueProposal>(stream,
        cancellationToken: cancellationToken);

      if (add != null) ret.Add(add);
    });

    await Task.WhenAll(tasks);

    return ret.ToArray();
  }

  // todo: better do outside at migrations
  private async Task CreateProposalsIndexIfNotExists(SearchCommands ft)
  {
    var idxExists = (await ft._ListAsync()).Any(x =>
    {
      if (x.Resp2Type != ResultType.SimpleString)
        return false;

      return (string?)x == "idx:proposals";
    });
    if (idxExists)
      return;

    var indexCreateResult = await ft.CreateAsync("idx:proposals",
      FTCreateParams.CreateParams().Prefix("proposal:"),
      new Schema()
        .AddTextField("origin")
        .AddNumericField("origin_date_time")
        .AddTextField("destination")
        .AddNumericField("destination_date_time")
        .AddNumericField("price")
        .AddNumericField("valid_until")
    );

    if (indexCreateResult)
      _logger.LogInformation("idx:proposals has been created");
  }

  public async Task AddProposals(string providerId, ProviderRouteValueProposal[] valueProposals,
    CancellationToken cancellationToken)
  {
    await _database.SetAddAsync($"provider:{providerId}:proposals-ids",
      valueProposals.Select(x => new RedisValue(x.Id.ToString())).ToArray());

    foreach (var proposal in valueProposals)
    {
      using var ms = new MemoryStream();
      await JsonSerializer.SerializeAsync(ms, proposal, cancellationToken: cancellationToken);

      await _database.HashSetAsync($"proposal:{proposal.Id}", new HashEntry[]
      {
        new("proposal", Encoding.UTF8.GetString(ms.ToArray())),
        new("provider_id", providerId),
        new("origin", proposal.RouteSegments.First().Origin.LocationName),
        new("origin_date_time", proposal.RouteSegments.First().Origin.LocationDate.Ticks),
        new("destination", proposal.RouteSegments.Last().Destination.LocationName),
        new("destination_date_time", proposal.RouteSegments.Last().Destination.LocationDate.Ticks),
        new("price", Convert.ToInt64(proposal.RouteSegmentsTravelPrices.Sum())),
        new("valid_until", proposal.ValidUntil.Ticks)
      });
    }
  }
}