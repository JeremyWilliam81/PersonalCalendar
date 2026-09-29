using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class AllDayEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AllDayEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Post_OneDayAllDayEvent_Returns201WithDatesOnly()
    {
        var response = await _client.PostAsJsonAsync("/api/events", AllDayInput("Birthday", "2026-10-14", "2026-10-14"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.True(body.GetProperty("isAllDay").GetBoolean());
        Assert.Equal("2026-10-14", body.GetProperty("startDate").GetString());
        Assert.Equal("2026-10-14", body.GetProperty("endDate").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("start").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("end").ValueKind);
    }

    [Fact]
    public async Task Post_EndDateBeforeStartDate_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/events", AllDayInput("Vacation", "2026-10-23", "2026-10-20"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["endDate.beforeStart"], (await response.ReadJsonAsync()).ErrorCodesFor("endDate"));
    }

    [Fact]
    public async Task GetMonth_ShowsMultiDayAllDayEventOnEachDay_WithDatesOnly()
    {
        await _client.CreateAsync(AllDayInput("Vacation", "2026-10-20", "2026-10-23"));

        var month = await _client.GetMonthAsync(2026, 10);

        Assert.Equal(["2026-10-20", "2026-10-21", "2026-10-22", "2026-10-23"], month.DaysWithTitle("Vacation"));
        var summary = month.Days().Single(d => d.GetProperty("date").GetString() == "2026-10-21").GetProperty("events")[0];
        Assert.True(summary.GetProperty("isAllDay").GetBoolean());
        Assert.Equal("2026-10-20", summary.GetProperty("startDate").GetString());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("start").ValueKind);
    }

    [Fact]
    public async Task Put_SwitchingTimedEventToAllDay_ReplacesTimesWithDates()
    {
        var id = (await _client.CreateAsync(TimedInput())).GetProperty("id").GetString();

        var response = await _client.PutAsJsonAsync($"/api/events/{id}", AllDayInput("Dentist", "2026-10-14", "2026-10-14", version: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.True(body.GetProperty("isAllDay").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("start").ValueKind);
        Assert.Equal("2026-10-14", body.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task AllDayEvent_KeepsItsDates_WhenViewedFromAnotherZone()
    {
        await _client.CreateAsync(AllDayInput("Birthday", "2026-10-20", "2026-10-20", timeZone: Chicago));

        var month = await _client.GetMonthAsync(2026, 10, "Asia/Tokyo");

        Assert.Equal(["2026-10-20"], month.DaysWithTitle("Birthday"));
    }
}
