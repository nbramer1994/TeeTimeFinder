namespace TeeTimeFinder.Models;

public class TeeTime
{
    public string CourseId { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public int AvailableSpots { get; set; }

    public int Holes { get; set; }

    public decimal? Price { get; set; }

    public string BookingUrl { get; set; } = string.Empty;
}
