using System.Net;
using System.Net.Http.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class SaveFailureTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose()
    {
        if (File.Exists(_factory.DatabasePath)) File.SetAttributes(_factory.DatabasePath, FileAttributes.Normal);
        _factory.Dispose();
    }

    [Fact]
    public async Task Post_WhenDatabaseCannotBeWritten_Returns500SaveFailed_AndStoresNothing()
    {
        var client = _factory.CreateClient(); // creates the database
        File.SetAttributes(_factory.DatabasePath, FileAttributes.ReadOnly);

        var response = await client.PostAsJsonAsync("/api/events", TimedInput());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("save-failed", (await response.ReadJsonAsync()).GetProperty("type").GetString());

        File.SetAttributes(_factory.DatabasePath, FileAttributes.Normal);
        Assert.Empty((await client.GetMonthAsync(2026, 10)).DaysWithTitle("Dentist"));
    }

    [Fact]
    public async Task Put_Following_WhenDatabaseCannotBeWritten_LeavesTheSeriesUnchanged()
    {
        var client = _factory.CreateClient();
        var created = await client.CreateAsync(new
        {
            title = "Gym", location = (string?)null, notes = (string?)null, isAllDay = false, timeZone = Chicago,
            start = "2026-10-12T07:00", end = "2026-10-12T08:00", startDate = (string?)null, endDate = (string?)null,
            acceptAdjustedTimes = false, version = (int?)null,
            recurrence = new { frequency = "daily", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)null, end = new { type = "never" } },
        });
        File.SetAttributes(_factory.DatabasePath, FileAttributes.ReadOnly);

        var response = await client.PutAsJsonAsync(
            $"/api/events/{created.GetProperty("id").GetString()}?occurrence=2026-10-20&scope=following",
            new
            {
                title = "Run", location = (string?)null, notes = (string?)null, isAllDay = false, timeZone = Chicago,
                start = "2026-10-20T07:00", end = "2026-10-20T08:00", startDate = (string?)null, endDate = (string?)null,
                acceptAdjustedTimes = false, version = created.GetProperty("version").GetInt32(),
                recurrence = new { frequency = "daily", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)null, end = new { type = "never" } },
            });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        File.SetAttributes(_factory.DatabasePath, FileAttributes.Normal);
        var october = await client.GetMonthAsync(2026, 10);
        Assert.Empty(october.DaysWithTitle("Run"));
        Assert.Contains("2026-10-31", october.DaysWithTitle("Gym"));
    }
}
