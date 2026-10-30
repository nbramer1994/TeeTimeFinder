using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using TeeTimeFinder.Core.Interfaces;
using TeeTimeFinder.Models;

namespace TeeTimeFinder.Api.Services;

public class EagleClubTeeTimeProvider(HttpClient http) : ITeeTimeProvider
{
    private const string ApiUrl = "https://api.EagleClubSystems.online";

    public BookingSystem System => BookingSystem.EagleClub;

    public async Task<IReadOnlyList<TeeTime>> GetTeeTimesAsync(CourseInfo course, DateOnly date, int players, CancellationToken cancellationToken = default)
    {
        var teePriceClassId = int.Parse(course.ScheduleId, CultureInfo.InvariantCulture);
        var carriageId = int.Parse(course.BookingClassId ?? "0", CultureInfo.InvariantCulture);

        var payload = new
        {
            BCC = new
            {
                StrServer = "GSERVER",
                StrURL = ApiUrl,
                StrDatabase = course.ExternalId,
                IntOrganizationID = 1,
                IntOperatorID = 2,
                FromProgram = "BE",
                LsteInterfaceID = Array.Empty<int>()
            },
            StrDate = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            StrTime = "0000",
            TeePriceClassID = teePriceClassId,
            IncludeExisting = false,
            Master_CarriageID = carriageId,
            Master_TeePriceClassIDs = $",{teePriceClassId},",
            OnlineBookingFormat = 0,
            OnlineBookingMaxDays = 7
        };

        using var response = await http.PostAsJsonAsync($"{ApiUrl}/api/online/OnlineAppointmentRetrieve", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var results = new List<TeeTime>();
        var root = doc.RootElement;
        if (root.TryGetProperty("BG", out var bg) && bg.TryGetProperty("BoolSuccess", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var message = bg.TryGetProperty("StrResult", out var r) ? r.GetString() : null;
            throw new InvalidOperationException($"Eagle Club error: {message}");
        }

        if (!root.TryGetProperty("LstAppointment", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        var dateKey = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var byTime = new SortedDictionary<string, (int Spots, decimal? Price)>();

        foreach (var item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("Sport", out var sport) || sport.GetString() != "Golf" ||
                !item.TryGetProperty("Date", out var d) || d.GetString() != dateKey ||
                JsonHelper.GetInt(item, "ReserveOnline") != 1)
            {
                continue;
            }

            var spots = JsonHelper.GetInt(item, "Slots");
            var time = item.GetProperty("Time").GetString();
            if (spots <= 0 || time is null)
            {
                continue;
            }

            var fee = JsonHelper.GetDecimal(item, "EighteenFee");
            if (byTime.TryGetValue(time, out var existing))
            {
                byTime[time] = (Math.Max(existing.Spots, spots), existing.Price is null ? fee : fee is null ? existing.Price : Math.Min(existing.Price.Value, fee.Value));
            }
            else
            {
                byTime[time] = (spots, fee);
            }
        }

        foreach (var (time, value) in byTime)
        {
            if (!TimeOnly.TryParseExact(time, "HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            {
                continue;
            }

            results.Add(new TeeTime
            {
                CourseId = course.Id,
                CourseName = course.Name,
                StartTime = date.ToDateTime(t),
                AvailableSpots = value.Spots,
                Holes = 18,
                Price = value.Price,
                BookingUrl = course.BookingUrl
            });
        }

        return results;
    }
}
