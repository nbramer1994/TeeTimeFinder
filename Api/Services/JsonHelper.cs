using System.Globalization;
using System.Text.Json;

namespace TeeTimeFinder.Api.Services;

internal static class JsonHelper
{
    public static int GetInt(JsonElement element, string name)
    {
        var value = GetDecimal(element, name);
        return value.HasValue ? (int)value.Value : 0;
    }

    public static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }
}
