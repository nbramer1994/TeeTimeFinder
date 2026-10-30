using Microsoft.AspNetCore.Mvc;
using TeeTimeFinder.Core.Interfaces;
using TeeTimeFinder.Models;

namespace TeeTimeFinder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeeTimesController(ITeeTimeSearchService searchService) : ControllerBase
{
    [HttpGet("courses")]
    public ActionResult<IReadOnlyList<CourseInfo>> GetCourses() => Ok(searchService.GetCourses());

    [HttpGet]
    public async Task<ActionResult<TeeTimeSearchResult>> Search(
        [FromQuery] DateOnly date,
        [FromQuery] string[]? courses = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await searchService.SearchAsync(date, 0, courses, cancellationToken));
    }
}
