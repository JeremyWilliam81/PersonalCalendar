using System.Net;
using System.Text.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

/// <summary>GET /api/calendar/days (specs/002-calendar-views/contracts/http-api.md).</summary>
public class DaysEndpointTests
{
    private static string Url(string start, int count, string timeZone = Chicago) =>
        $"/api/calendar/days?timeZone={Uri.EscapeDataString(timeZone)}&start={start}&count={count}";

    [Fact]
    public async Task Week_ReturnsTheContractShape()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await client.CreateAsync(TimedInput("Dentist", "2026-10-14T09:00", "2026-10-14T10:30"));
        await client.CreateAsync(TimedInput("Late show", "2026-10-14T22:00", "2026-10-15T01:00"));
        await client.CreateAsync(AllDayInput("Trip", "2026-10-16", "2026-10-20"));

        var response = await client.GetAsync(Url("2026-10-11", 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await response.ReadJsonAsync();
        Assert.Equal(Chicago, view.GetProperty("timeZone").GetString());
        Assert.Equal("2026-09-29", view.GetProperty("today").GetString());
        var days = view.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);

        var oct14 = days[3];
        Assert.Equal("2026-10-14", oct14.GetProperty("date").GetString());
        Assert.Equal("2026-10-14T00:00:00-05:00", oct14.GetProperty("dayStart").GetString());
        Assert.Equal("2026-10-15T00:00:00-05:00", oct14.GetProperty("dayEnd").GetString());
        Assert.Equal(1440, oct14.GetProperty("lengthMinutes").GetInt32());
        Assert.Equal("00:00", oct14.GetProperty("hourMarks")[0].GetProperty("label").GetString());

        var dentist = oct14.GetProperty("timed").EnumerateArray().Single(s => Title(s) == "Dentist");
        Assert.Equal(540, dentist.GetProperty("offsetMinutes").GetInt32());
        Assert.Equal(90, dentist.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("2026-10-14T09:00:00-05:00", dentist.GetProperty("event").GetProperty("start").GetString());

        var late = days[4].GetProperty("timed").EnumerateArray().Single(s => Title(s) == "Late show");
        Assert.True(late.GetProperty("continuesBefore").GetBoolean());
        Assert.Equal("2026-10-14T22:00:00-05:00", late.GetProperty("event").GetProperty("start").GetString());

        var trip = view.GetProperty("allDayBars").EnumerateArray().Single();
        Assert.Equal(5, trip.GetProperty("startIndex").GetInt32());
        Assert.Equal(2, trip.GetProperty("span").GetInt32());
        Assert.True(trip.GetProperty("continuesAfter").GetBoolean());
        Assert.Equal("Trip", days[5].GetProperty("allDay")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task FallBackDay_Is1500MinutesLong_WithTwoOneOClockMarks()
    {
        using var factory = new ApiFactory();

        var view = await (await factory.CreateClient().GetAsync(Url("2026-11-01", 1))).ReadJsonAsync();

        var day = view.GetProperty("days")[0];
        Assert.Equal(1500, day.GetProperty("lengthMinutes").GetInt32());
        Assert.Equal(2, day.GetProperty("hourMarks").EnumerateArray().Count(m => m.GetProperty("label").GetString() == "01:00"));
        Assert.Empty(view.GetProperty("allDayBars").EnumerateArray());
    }

    [Fact]
    public async Task InvalidParameters_Return400WithCodes()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var zone = await client.GetAsync(Url("2026-10-14", 1, "Mars/Base"));
        var count = await client.GetAsync(Url("2026-10-14", 3));

        Assert.Equal(HttpStatusCode.BadRequest, zone.StatusCode);
        var zoneProblem = await zone.ReadJsonAsync();
        Assert.Equal("validation", zoneProblem.GetProperty("type").GetString());
        Assert.Equal(["timeZone.unknown"], zoneProblem.ErrorCodesFor("timeZone"));
        Assert.Equal(["count.invalid"], (await count.ReadJsonAsync()).ErrorCodesFor("count"));
    }

    private static string? Title(JsonElement segment) => segment.GetProperty("event").GetProperty("title").GetString();
}
