using TeeTimeFinder.Models;

namespace TeeTimeFinder.Core.Interfaces;

public interface ITeeTimeSearchService
{
    IReadOnlyList<CourseInfo> GetCourses();

    Task<TeeTimeSearchResult> SearchAsync(DateOnly date, int players, IEnumerable<string>? courseIds, CancellationToken cancellationToken = default);
}
