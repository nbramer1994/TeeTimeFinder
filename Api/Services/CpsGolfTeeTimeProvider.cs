using System.Globalization;
using System.Text;
using System.Text.Json;
using TeeTimeFinder.Core.Interfaces;
using TeeTimeFinder.Models;

namespace TeeTimeFinder.Api.Services;

public class CpsGolfTeeTimeProvider(HttpClient http) : ITeeTimeProvider
{
    private const string ApiKey = "8ea2914e-cac2-48a7-a3e5-e0f41350bf3a";

    public BookingSystem System => BookingSystem.CpsGolf;

    public async Task<IReadOnlyList<TeeTime>> GetTeeTimesAsync(CourseInfo course, DateOnly date, int players, CancellationToken cancellationToken = default)
    {
        var host = $"https://{course.Alias}.cps.golf";

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{host}/identityapi/myconnect/token/short")
        {
            Content = new StringContent("client_id=onlinereswebshortlived", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        using var tokenResponse = await http.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenDoc = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var token = tokenDoc.RootElement.GetProperty("access_token").GetString();

        var baseUrl = $"{host}/onlineres/onlineapi/api/v1/onlinereservation";
        var transactionId = Guid.NewGuid().ToString();

        using (var register = CreateRequest(HttpMethod.Post, $"{baseUrl}/RegisterTransactionId", host, token, course))
        {
            register.Content = new StringContent($"{{\"transactionId\":\"{transactionId}\"}}", Encoding.UTF8, "application/json");
            using var registerResponse = await http.SendAsync(register, cancellationToken);
            registerResponse.EnsureSuccessStatusCode();
        }

        var searchDate = Uri.EscapeDataString(date.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture));
        var url = $"{baseUrl}/TeeTimes?searchDate={searchDate}&holes=0&numberOfPlayer=0&courseIds={course.ScheduleId}" +
                  $"&searchTimeType=0&transactionId={transactionId}&teeOffTimeMin=0&teeOffTimeMax=23&isChangeTeeTime=false" +
                  "&teeSheetSearchView=5&classCode=R&defaultOnlineRate=N&isUseCapacityPricing=false&memberStoreId=1&searchType=1";

        using var request = CreateRequest(HttpMethod.Get, url, host, token, course);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var results = new List<TeeTime>();
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var slot in content.EnumerateArray())
        {
            if (!slot.TryGetProperty("startTime", out var st) || !st.TryGetDateTime(out var start))
            {
                continue;
            }

            var spots = JsonHelper.GetInt(slot, "participants");
            if (spots < players)
            {
                continue;
            }

            var holes = JsonHelper.GetInt(slot, "defaultHoles");
            if (holes == 0)
            {
                holes = JsonHelper.GetInt(slot, "holes");
            }

            decimal? price = null;
            if (slot.TryGetProperty("shItemPrices", out var prices) && prices.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in prices.EnumerateArray())
                {
                    var code = p.TryGetProperty("shItemCode", out var c) ? c.GetString() : null;
                    if (code == $"GreenFee{holes}")
                    {
                        price = JsonHelper.GetDecimal(p, "price");
                        break;
                    }
                }
            }

            results.Add(new TeeTime
            {
                CourseId = course.Id,
                CourseName = course.Name,
                StartTime = start,
                AvailableSpots = spots,
                Holes = holes,
                Price = price,
                BookingUrl = course.BookingUrl
            });
        }

        return results;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string host, string? token, CourseInfo course)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        request.Headers.TryAddWithoutValidation("x-componentid", "1");
        request.Headers.TryAddWithoutValidation("x-moduleid", "7");
        request.Headers.TryAddWithoutValidation("x-productid", "1");
        request.Headers.TryAddWithoutValidation("x-terminalid", "3");
        request.Headers.TryAddWithoutValidation("x-siteid", course.ExternalId);
        request.Headers.TryAddWithoutValidation("x-ismobile", "false");
        request.Headers.TryAddWithoutValidation("client-id", "onlineresweb");
        request.Headers.TryAddWithoutValidation("x-apikey", ApiKey);
        request.Headers.TryAddWithoutValidation("Origin", host);
        request.Headers.TryAddWithoutValidation("Referer", $"{host}/onlineresweb/search-teetime");
        return request;
    }
}
