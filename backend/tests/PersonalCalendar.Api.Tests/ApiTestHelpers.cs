using System.Net.Http.Json;
using System.Text.Json;

namespace PersonalCalendar.Api.Tests;

internal static class ApiTestHelpers
{
    public const string Chicago = "America/Chicago";

    public static object TimedInput(
        string title = "Dentist",
        string start = "2026-10-14T09:00",
        string end = "2026-10-14T10:00",
        string timeZone = Chicago,
        bool acceptAdjustedTimes = false,
        string? location = null,
        string? notes = null,
        int? version = null) => new
    {
        title,
        location,
        notes,
        isAllDay = false,
        timeZone,
        start,
        end,
        startDate = (string?)null,
        endDate = (string?)null,
        acceptAdjustedTimes,
        version,
    };

    public static object AllDayInput(
        string title,
        string startDate,
        string endDate,
        string timeZone = Chicago,
        int? version = null) => new
    {
        title,
        location = (string?)null,
        notes = (string?)null,
        isAllDay = true,
        timeZone,
        start = (string?)null,
        end = (string?)null,
        startDate,
        endDate,
        acceptAdjustedTimes = false,
        version,
    };

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<JsonElement> CreateAsync(this HttpClient client, object input)
    {
        var response = await client.PostAsJsonAsync("/api/events", input);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    public static async Task<JsonElement> GetMonthAsync(this HttpClient client, int year, int month, string timeZone = Chicago)
    {
        var response = await client.GetAsync($"/api/calendar/month?year={year}&month={month}&timeZone={Uri.EscapeDataString(timeZone)}");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    public static IEnumerable<JsonElement> Days(this JsonElement monthView) =>
        monthView.GetProperty("weeks").EnumerateArray().SelectMany(w => w.GetProperty("days").EnumerateArray());

    /// <summary>Dates (yyyy-MM-dd) of the grid days showing an event with this title.</summary>
    public static List<string> DaysWithTitle(this JsonElement monthView, string title) =>
        monthView.Days()
            .Where(d => d.GetProperty("events").EnumerateArray().Any(e => e.GetProperty("title").GetString() == title))
            .Select(d => d.GetProperty("date").GetString()!)
            .ToList();

    public static string[] ErrorCodesFor(this JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();
}
