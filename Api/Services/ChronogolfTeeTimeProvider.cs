using System.Globalization;
using System.Text.Json;
using TeeTimeFinder.Core.Interfaces;
using TeeTimeFinder.Models;

namespace TeeTimeFinder.Api.Services;

public class ChronogolfTeeTimeProvider(HttpClient http) : ITeeTimeProvider
{
    private const string PublicAffiliationTypeId = "99680";

    public BookingSystem System => BookingSystem.Chronogolf;

    public async Task<IReadOnlyList<TeeTime>> GetTeeTimesAsync(CourseInfo course, DateOnly date, int players, CancellationToken cancellationToken = default)
    {
        var url = $"https://www.chronogolf.com/marketplace/clubs/{course.ExternalId}/teetimes" +
                  $"?date={date:yyyy-MM-dd}&course_id={course.ScheduleId}&affiliation_type_ids%5B%5D={PublicAffiliationTypeId}&nb_holes=18";

        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var results = new List<TeeTime>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (IsTrue(item, "out_of_capacity") || IsTrue(item, "frozen"))
            {
                continue;
            }

            var time = item.TryGetProperty("start_time", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (time is null || !TimeOnly.TryParse(time, CultureInfo.InvariantCulture, out var timeOnly))
            {
                continue;
            }

            var spots = JsonHelper.GetInt(item, "available_spots");
            if (spots <= 0)
            {
                spots = 4;
            }

            results.Add(new TeeTime
            {
                CourseId = course.Id,
                CourseName = course.Name,
                StartTime = date.ToDateTime(timeOnly),
                AvailableSpots = spots,
                Holes = 18,
                Price = JsonHelper.GetDecimal(item, "price"),
                BookingUrl = course.BookingUrl
            });
        }

        return results;
    }

    private static bool IsTrue(JsonElement item, string name) =>
        item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
}
