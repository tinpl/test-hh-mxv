using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TestTask;
using WebApi.Services;

namespace WebApi.Controllers
{
    // assume: checked for RPM
    // assume: throttled
    // assume: authorized

  [ApiController]
  [Route("[controller]/[action]")]
  public class SearchRoutesController: ControllerBase, ISearchService
  {
    private readonly ILogger<SearchRoutesController> _logger;
    private readonly IRoutesSearchService _searchService;

    public SearchRoutesController(IRoutesSearchService searchService,
      ILogger<SearchRoutesController> logger)
    {
      _logger = logger;
      _searchService = searchService;
    }

    [HttpPost(Name = "Search")]
    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
      // todo: utilize Request.Headers["X-Request-ID"] if available?
      long requestId = Sample.Utilities.RequestIdGenerator.Next();

      return await _searchService.SearchAsync("user-1", requestId, request, cancellationToken);
    }

    [HttpGet(Name = "ping")]
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
      return await _searchService.IsAvailableAsync("user-1", cancellationToken);
    }
  }
}
