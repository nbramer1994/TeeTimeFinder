using System.Globalization;
using System.Text.Json;
using TeeTimeFinder.Core.Interfaces;
using TeeTimeFinder.Models;

namespace TeeTimeFinder.Api.Services;

public class TeeItUpTeeTimeProvider(HttpClient http) : ITeeTimeProvider
{
    private static readonly Lazy<TimeZoneInfo> Eastern = new(() =>
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Detroit"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    });

    public BookingSystem System => BookingSystem.TeeItUp;

    public async Task<IReadOnlyList<TeeTime>> GetTeeTimesAsync(CourseInfo course, DateOnly date, int players, CancellationToken cancellationToken = default)
    {
        var url = $"https://phx-api-be-east-1b.kenna.io/v2/tee-times?date={date:yyyy-MM-dd}&facilityIds={course.ScheduleId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-be-alias", course.Alias);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var results = new List<TeeTime>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var day in doc.RootElement.EnumerateArray())
        {
            if (!day.TryGetProperty("teetimes", out var slots) || slots.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var slot in slots.EnumerateArray())
            {
                if (!slot.TryGetProperty("teetime", out var tt) ||
                    !DateTimeOffset.TryParse(tt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var utc))
                {
                    continue;
                }

                var spots = JsonHelper.GetInt(slot, "maxPlayers") - JsonHelper.GetInt(slot, "bookedPlayers");
                if (spots < players)
                {
                    continue;
                }

                decimal? price = null;
                var holes = 0;
                if (slot.TryGetProperty("rates", out var rates) && rates.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rate in rates.EnumerateArray())
                    {
                        var cents = JsonHelper.GetDecimal(rate, "greenFeeCart");
                        if (rate.TryGetProperty("promotion", out var promo) && promo.ValueKind == JsonValueKind.Object)
                        {
                            cents = JsonHelper.GetDecimal(promo, "greenFeeCart") ?? cents;
                        }

                        if (cents.HasValue && (price is null || cents.Value / 100m < price))
                        {
                            price = cents.Value / 100m;
                            holes = JsonHelper.GetInt(rate, "holes");
                        }
                    }
                }

                results.Add(new TeeTime
                {
                    CourseId = course.Id,
                    CourseName = course.Name,
                    StartTime = TimeZoneInfo.ConvertTime(utc, Eastern.Value).DateTime,
                    AvailableSpots = spots,
                    Holes = holes,
                    Price = price,
                    BookingUrl = course.BookingUrl
                });
            }
        }

        return results;
    }
}
