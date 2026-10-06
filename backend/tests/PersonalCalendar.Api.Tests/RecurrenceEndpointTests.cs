using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

/// <summary>specs/003-recurring-events/contracts/http-api.md.</summary>
public sealed class RecurrenceEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public RecurrenceEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static object Gym(object? recurrence = null, string start = "2026-10-12T07:00", string end = "2026-10-12T08:00", string title = "Gym", int? version = null) => new
    {
        title,
        location = (string?)null,
        notes = (string?)null,
        isAllDay = false,
        timeZone = Chicago,
        start,
        end,
        startDate = (string?)null,
        endDate = (string?)null,
        acceptAdjustedTimes = false,
        version,
        recurrence = recurrence ?? MwfWeekly(),
    };

    private static object MwfWeekly(object? end = null) => new
    {
        frequency = "weekly",
        interval = 1,
        weekdays = new[] { "monday", "wednesday", "friday" },
        monthly = (object?)null,
        end = end ?? new { type = "never" },
    };

    // ----- Create and view (US1) -----

    [Fact]
    public async Task Post_WithRecurrence_Returns201WithTheRule()
    {
        var response = await _client.PostAsJsonAsync("/api/events", Gym());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("2026-10-12", body.GetProperty("occurrenceDate").GetString());
        var recurrence = body.GetProperty("recurrence");
        Assert.Equal("weekly", recurrence.GetProperty("frequency").GetString());
        Assert.Equal(1, recurrence.GetProperty("interval").GetInt32());
        Assert.Equal(["monday", "wednesday", "friday"], recurrence.GetProperty("weekdays").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal("never", recurrence.GetProperty("end").GetProperty("type").GetString());
        Assert.Equal(Chicago, recurrence.GetProperty("timeZone").GetString());
    }

    public static TheoryData<string, string, string> InvalidRules => new()
    {
        { """{"frequency":"daily","interval":0,"weekdays":[],"monthly":null,"end":{"type":"never"}}""", "recurrence.interval", "recurrence.interval.outOfRange" },
        { """{"frequency":"weekly","interval":1,"weekdays":[],"monthly":null,"end":{"type":"never"}}""", "recurrence.weekdays", "recurrence.weekdays.required" },
        { """{"frequency":"daily","interval":1,"weekdays":[],"monthly":null,"end":{"type":"count","count":1000}}""", "recurrence.count", "recurrence.count.outOfRange" },
        { """{"frequency":"daily","interval":1,"weekdays":[],"monthly":null,"end":{"type":"until","until":"2026-10-01"}}""", "recurrence.until", "recurrence.until.beforeStart" },
        { """{"frequency":"monthly","interval":1,"weekdays":[],"monthly":{"type":"weekdayPosition","ordinal":3},"end":{"type":"never"}}""", "recurrence.monthly", "recurrence.monthly.invalid" },
        { """{"frequency":"hourly","interval":1,"weekdays":[],"monthly":null,"end":{"type":"never"}}""", "recurrence.frequency", "recurrence.frequency.invalid" },
    };

    [Theory]
    [MemberData(nameof(InvalidRules))]
    public async Task Post_WithAnInvalidRule_Returns400WithTheFieldCode(string recurrenceJson, string field, string code)
    {
        var body = JsonSerializer.Serialize(Gym()).Replace(
            JsonSerializer.Serialize(MwfWeekly()), recurrenceJson, StringComparison.Ordinal);

        var response = await _client.PostAsync("/api/events", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, (await response.ReadJsonAsync()).ErrorCodesFor(field));
    }

    [Fact]
    public async Task MonthView_IncludesOccurrences_WithTheirKeys()
    {
        await _client.CreateAsync(Gym());

        var month = await _client.GetMonthAsync(2026, 10);

        Assert.Equal(
            ["2026-10-12", "2026-10-14", "2026-10-16", "2026-10-19", "2026-10-21", "2026-10-23", "2026-10-26", "2026-10-28", "2026-10-30"],
            month.DaysWithTitle("Gym").Where(d => d.StartsWith("2026-10", StringComparison.Ordinal)));
        var occurrence = month.Days().Single(d => d.GetProperty("date").GetString() == "2026-10-21").GetProperty("events")[0];
        Assert.True(occurrence.GetProperty("isRecurring").GetBoolean());
        Assert.Equal("2026-10-21", occurrence.GetProperty("occurrenceDate").GetString());
        Assert.Equal("2026-10-21T07:00:00-05:00", occurrence.GetProperty("start").GetString());
    }

    [Fact]
    public async Task DaysView_IncludesOccurrences()
    {
        await _client.CreateAsync(Gym());

        var response = await _client.GetAsync($"/api/calendar/days?start=2026-10-18&count=7&timeZone={Chicago}");
        var days = (await response.ReadJsonAsync()).GetProperty("days").EnumerateArray().ToList();

        Assert.Equal([1, 3, 5], days.Select((d, i) => (d, i)).Where(x => x.d.GetProperty("timed").GetArrayLength() == 1).Select(x => x.i));
        var segment = days[3].GetProperty("timed")[0].GetProperty("event");
        Assert.Equal("2026-10-21", segment.GetProperty("occurrenceDate").GetString());
    }

    [Fact]
    public async Task Series_SurvivesARestart()
    {
        await _client.CreateAsync(Gym(MwfWeekly(new { type = "count", count = 6 })));

        using var restarted = _factory.CreateRestartedFactory();
        var month = await restarted.CreateClient().GetMonthAsync(2026, 10);

        Assert.Equal(
            ["2026-10-12", "2026-10-14", "2026-10-16", "2026-10-19", "2026-10-21", "2026-10-23"],
            month.DaysWithTitle("Gym"));
    }

    // ----- Occurrence details (US2) -----

    [Fact]
    public async Task GetOccurrence_ReturnsThatOccurrenceWithTheSeries()
    {
        var id = (await _client.CreateAsync(Gym())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence=2026-10-21");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("2026-10-21T07:00:00-05:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-10-21T08:00:00-05:00", body.GetProperty("end").GetString());
        Assert.Equal("2026-10-21", body.GetProperty("occurrenceDate").GetString());
        Assert.Equal("2026-10-12T07:00:00-05:00", body.GetProperty("seriesStart").GetString());
        Assert.Equal("weekly", body.GetProperty("recurrence").GetProperty("frequency").GetString());
        Assert.Equal(0, body.GetProperty("exceptionCount").GetInt32());
        Assert.False(body.GetProperty("isException").GetBoolean());
    }

    [Fact]
    public async Task GetSeries_WithoutOccurrence_Returns400()
    {
        var id = (await _client.CreateAsync(Gym())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("occurrence.required", (await response.ReadJsonAsync()).ErrorCodesFor("occurrence"));
    }

    [Fact]
    public async Task GetOneTimeEvent_WithOccurrence_Returns400()
    {
        var id = (await _client.CreateAsync(TimedInput())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence=2026-10-14");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("occurrence.invalid", (await response.ReadJsonAsync()).ErrorCodesFor("occurrence"));
    }

    [Theory]
    [InlineData("2026-10-20")] // a Tuesday: not produced by M/W/F
    [InlineData("2026-10-05")] // before the series starts
    public async Task GetOccurrence_ThatTheRuleDoesNotProduce_Returns404(string date)
    {
        var id = (await _client.CreateAsync(Gym())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence={date}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOccurrence_WithAnUnreadableDate_Returns400()
    {
        var id = (await _client.CreateAsync(Gym())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence=2026-02-30");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("occurrence.invalid", (await response.ReadJsonAsync()).ErrorCodesFor("occurrence"));
    }

    [Fact]
    public async Task OneTimeEvent_HasNoRecurrenceFields()
    {
        var created = await _client.CreateAsync(TimedInput());

        Assert.Equal(JsonValueKind.Null, created.GetProperty("recurrence").ValueKind);
        var summary = (await _client.GetMonthAsync(2026, 10)).Days().SelectMany(d => d.GetProperty("events").EnumerateArray()).Single();
        Assert.False(summary.GetProperty("isRecurring").GetBoolean());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("occurrenceDate").ValueKind);
    }

    // ----- Editing with a scope (US3) -----

    private async Task<(string Id, int Version)> CreateGymAsync()
    {
        var created = await _client.CreateAsync(Gym());
        return (created.GetProperty("id").GetString()!, created.GetProperty("version").GetInt32());
    }

    private Task<HttpResponseMessage> PutAsync(string id, string query, object body) =>
        _client.PutAsJsonAsync($"/api/events/{id}{query}", body);

    public static TheoryData<string, string, object?, string, string> PutRefusals => new()
    {
        { "", "2026-10-21", null, "occurrence", "occurrence.required" },
        { "?occurrence=2026-10-21", "2026-10-21", null, "scope", "scope.required" },
        { "?occurrence=2026-10-21&scope=some", "2026-10-21", null, "scope", "scope.invalid" },
        { "?occurrence=2026-10-21&scope=this", "2026-10-21", "monday", "scope", "scope.thisWithRepeatChange" },
        { "?occurrence=2026-10-21&scope=all", "2026-10-22", null, "scope", "scope.dateChangeRequiresThis" },
        { "?occurrence=2026-10-21&scope=following", "2026-10-22", null, "scope", "scope.dateChangeRequiresThis" },
        { "?occurrence=2026-10-21&scope=this", "2026-10-22", "monday", "start", "scope.dateAndRepeatChanged" },
    };

    [Theory]
    [MemberData(nameof(PutRefusals))]
    public async Task Put_OnASeries_RefusesTheContractCases(string query, string date, object? onlyWeekday, string field, string code)
    {
        var (id, version) = await CreateGymAsync();
        var recurrence = onlyWeekday is string day
            ? new { frequency = "weekly", interval = 1, weekdays = new[] { day }, monthly = (object?)null, end = new { type = "never" } }
            : MwfWeekly();

        var response = await PutAsync(id, query, Gym(recurrence, $"{date}T07:00", $"{date}T08:00", version: version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, (await response.ReadJsonAsync()).ErrorCodesFor(field));
    }

    [Theory]
    [InlineData("?occurrence=2026-10-14", "occurrence", "occurrence.invalid")]
    [InlineData("?scope=all", "scope", "scope.invalid")]
    public async Task Put_OnAOneTimeEvent_RejectsOccurrenceAndScope(string query, string field, string code)
    {
        var created = await _client.CreateAsync(TimedInput());

        var response = await PutAsync(created.GetProperty("id").GetString()!, query, TimedInput(version: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, (await response.ReadJsonAsync()).ErrorCodesFor(field));
    }

    [Fact]
    public async Task Put_This_ChangesOneOccurrence()
    {
        var (id, version) = await CreateGymAsync();

        var response = await PutAsync(id, "?occurrence=2026-10-21&scope=this", Gym(start: "2026-10-21T18:00", end: "2026-10-21T19:00", version: version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.True(body.GetProperty("isException").GetBoolean());
        var month = await _client.GetMonthAsync(2026, 10);
        var times = month.Days().SelectMany(d => d.GetProperty("events").EnumerateArray())
            .ToDictionary(e => e.GetProperty("occurrenceDate").GetString()!, e => e.GetProperty("start").GetString());
        Assert.Equal("2026-10-21T18:00:00-05:00", times["2026-10-21"]);
        Assert.Equal("2026-10-19T07:00:00-05:00", times["2026-10-19"]);
    }

    [Fact]
    public async Task Put_Following_ReturnsTheNewSeries_AndSplitsTheTitle()
    {
        var (id, version) = await CreateGymAsync();

        var response = await PutAsync(id, "?occurrence=2026-11-02&scope=following",
            Gym(start: "2026-11-02T07:00", end: "2026-11-02T08:00", title: "Swim", version: version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(id, (await response.ReadJsonAsync()).GetProperty("id").GetString());
        var november = await _client.GetMonthAsync(2026, 11);
        Assert.Equal("2026-11-02", november.DaysWithTitle("Swim").First());
        Assert.Empty(november.DaysWithTitle("Gym"));
        Assert.Contains("2026-10-30", (await _client.GetMonthAsync(2026, 10)).DaysWithTitle("Gym"));
    }

    [Fact]
    public async Task Put_WithAStaleVersion_Returns409()
    {
        var (id, version) = await CreateGymAsync();

        var response = await PutAsync(id, "?occurrence=2026-10-21&scope=all", Gym(title: "Swim", version: version + 5));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ----- Deleting with a scope (US4) -----

    private Task<HttpResponseMessage> DeleteAsync(string id, int version, string query) =>
        _client.DeleteAsync($"/api/events/{id}?version={version}{query}");

    [Theory]
    [InlineData("", "occurrence", "occurrence.required")]
    [InlineData("&occurrence=2026-10-21", "scope", "scope.required")]
    [InlineData("&occurrence=2026-10-21&scope=some", "scope", "scope.invalid")]
    public async Task Delete_OnASeries_RefusesTheContractCases(string query, string field, string code)
    {
        var (id, version) = await CreateGymAsync();

        var response = await DeleteAsync(id, version, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, (await response.ReadJsonAsync()).ErrorCodesFor(field));
    }

    [Fact]
    public async Task Delete_This_Then_All_SurvivesARestart()
    {
        var (id, version) = await CreateGymAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(id, version, "&occurrence=2026-10-21&scope=this")).StatusCode);
        using (var restarted = _factory.CreateRestartedFactory())
        {
            var october = await restarted.CreateClient().GetMonthAsync(2026, 10);
            Assert.DoesNotContain("2026-10-21", october.DaysWithTitle("Gym"));
            Assert.Contains("2026-10-23", october.DaysWithTitle("Gym"));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(id, version + 1, "&occurrence=2026-10-23&scope=all")).StatusCode);
        Assert.Empty((await _client.GetMonthAsync(2026, 10)).DaysWithTitle("Gym"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence=2026-10-23")).StatusCode);
    }

    [Fact]
    public async Task Delete_All_LeavesNoOrphanExceptions()
    {
        var (id, version) = await CreateGymAsync();
        await DeleteAsync(id, version, "&occurrence=2026-10-21&scope=this");

        await DeleteAsync(id, version + 1, "&occurrence=2026-10-23&scope=all");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalCalendar.Infrastructure.Persistence.CalendarDbContext>();
        Assert.Empty(db.OccurrenceExceptions);
    }

    [Fact]
    public async Task Delete_This_OnTheLastOccurrence_DeletesTheSeries()
    {
        var created = await _client.CreateAsync(Gym(MwfWeekly(new { type = "count", count = 1 })));
        var id = created.GetProperty("id").GetString()!;

        await DeleteAsync(id, created.GetProperty("version").GetInt32(), "&occurrence=2026-10-12&scope=this");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}&occurrence=2026-10-12")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns409()
    {
        var (id, version) = await CreateGymAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await DeleteAsync(id, version + 3, "&occurrence=2026-10-21&scope=this")).StatusCode);
    }

    // ----- Changing or stopping the repeat (US5) -----

    [Fact]
    public async Task Put_All_WithoutARule_TurnsTheSeriesIntoAOneTimeEvent()
    {
        var (id, version) = await CreateGymAsync();
        var body = JsonSerializer.Serialize(Gym(start: "2026-10-21T07:00", end: "2026-10-21T08:00", version: version))
            .Replace(JsonSerializer.Serialize(MwfWeekly()), "null", StringComparison.Ordinal);

        var response = await _client.PutAsync($"/api/events/{id}?occurrence=2026-10-21&scope=all",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await response.ReadJsonAsync()).GetProperty("recurrence").ValueKind);
        Assert.Equal(["2026-10-12"], (await _client.GetMonthAsync(2026, 10)).DaysWithTitle("Gym"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}")).StatusCode);
    }
}
