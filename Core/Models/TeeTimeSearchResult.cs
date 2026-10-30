namespace TeeTimeFinder.Models;

public class CourseSearchError
{
    public string CourseName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class TeeTimeSearchResult
{
    public List<TeeTime> TeeTimes { get; set; } = [];

    public List<CourseSearchError> Errors { get; set; } = [];
}
