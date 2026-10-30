using TeeTimeFinder.Models;

namespace TeeTimeFinder.Core.Interfaces;

public interface ITeeTimeProvider
{
    BookingSystem System { get; }

    Task<IReadOnlyList<TeeTime>> GetTeeTimesAsync(CourseInfo course, DateOnly date, int players, CancellationToken cancellationToken = default);
}
