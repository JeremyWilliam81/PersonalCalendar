using System.Net;
using System.Net.Http.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class CreateAndMonthViewEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public CreateAndMonthViewEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Post_ValidTimedEvent_Returns201WithContractBody()
    {
        var response = await _client.PostAsJsonAsync("/api/events", TimedInput(location: "Main St Clinic"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetString();
        Assert.Equal($"/api/events/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Dentist", body.GetProperty("title").GetString());
        Assert.Equal("Main St Clinic", body.GetProperty("location").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("notes").ValueKind);
        Assert.False(body.GetProperty("isAllDay").GetBoolean());
        Assert.Equal(Chicago, body.GetProperty("timeZone").GetString());
        Assert.Equal("2026-10-14T09:00:00-05:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-10-14T10:00:00-05:00", body.GetProperty("end").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Post_EndEqualToStart_Returns400WithFieldCode()
    {
        var response = await _client.PostAsJsonAsync("/api/events", TimedInput(end: "2026-10-14T09:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("validation", problem.GetProperty("type").GetString());
        Assert.Equal(["end.notAfterStart"], problem.ErrorCodesFor("end"));
    }

    [Fact]
    public async Task Post_WhitespaceTitle_Returns400TitleRequired()
    {
        var response = await _client.PostAsJsonAsync("/api/events", TimedInput(title: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["title.required"], (await response.ReadJsonAsync()).ErrorCodesFor("title"));
    }

    [Fact]
    public async Task Post_MalformedLocalTime_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/events", TimedInput(start: "tomorrow"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["start.invalid"], (await response.ReadJsonAsync()).ErrorCodesFor("start"));
    }

    [Fact]
    public async Task Post_TimeInDstGap_Returns422_ThenAcceptedResendReturns201()
    {
        var gap = TimedInput(start: "2027-03-14T02:30", end: "2027-03-14T04:00");

        var first = await _client.PostAsJsonAsync("/api/events", gap);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        var problem = await first.ReadJsonAsync();
        Assert.Equal("dst-adjustment-required", problem.GetProperty("type").GetString());
        Assert.Equal("2027-03-14T03:30", problem.GetProperty("adjustedStart").GetString());
        Assert.Equal("2027-03-14T04:00", problem.GetProperty("adjustedEnd").GetString());
        Assert.Equal(Chicago, problem.GetProperty("timeZone").GetString());

        var second = await _client.PostAsJsonAsync("/api/events",
            TimedInput(start: "2027-03-14T02:30", end: "2027-03-14T04:00", acceptAdjustedTimes: true));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("2027-03-14T03:30:00-05:00", (await second.ReadJsonAsync()).GetProperty("start").GetString());
    }

    [Fact]
    public async Task Post_UnknownTimeZone_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/events", TimedInput(timeZone: "Mars/Base"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["timeZone.unknown"], (await response.ReadJsonAsync()).ErrorCodesFor("timeZone"));
    }

    [Fact]
    public async Task GetMonth_ShowsCreatedEventOnItsDay()
    {
        await _client.CreateAsync(TimedInput());

        var month = await _client.GetMonthAsync(2026, 10);

        Assert.Equal(2026, month.GetProperty("year").GetInt32());
        Assert.Equal(10, month.GetProperty("month").GetInt32());
        Assert.Equal("2026-09-29", month.GetProperty("today").GetString());
        Assert.Equal(["2026-10-14"], month.DaysWithTitle("Dentist"));
        var summary = month.Days().Single(d => d.GetProperty("date").GetString() == "2026-10-14")
            .GetProperty("events")[0];
        Assert.Equal("2026-10-14T09:00:00-05:00", summary.GetProperty("start").GetString());
    }

    [Fact]
    public async Task GetMonth_WithoutYearAndMonth_ReturnsCurrentMonthOfClock()
    {
        var response = await _client.GetAsync($"/api/calendar/month?timeZone={Chicago}");

        var month = await response.ReadJsonAsync();
        Assert.Equal(2026, month.GetProperty("year").GetInt32());
        Assert.Equal(9, month.GetProperty("month").GetInt32());
    }

    [Fact]
    public async Task GetMonth_UnknownZoneOrMonthOutOfRange_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/calendar/month?year=2026&month=10&timeZone=Mars/Base")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync($"/api/calendar/month?year=2026&month=13&timeZone={Chicago}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/calendar/month?year=2026&month=10")).StatusCode);
    }

    [Fact]
    public async Task CreatedEvent_SurvivesRestart()
    {
        await _client.CreateAsync(TimedInput(location: "Main St Clinic", notes: "Bring card"));

        using var restarted = _factory.CreateRestartedFactory();
        var month = await restarted.CreateClient().GetMonthAsync(2026, 10);

        Assert.Equal(["2026-10-14"], month.DaysWithTitle("Dentist"));
    }
}
