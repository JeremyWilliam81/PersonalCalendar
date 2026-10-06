using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCalendar.Infrastructure.Persistence;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

/// <summary>A database created by feature 001 upgrades to recurring events with its events intact (task T012).</summary>
public sealed class MigrationUpgradeTests
{
    private const string TimedId = "11111111-1111-1111-1111-111111111111";
    private const string AllDayId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Database_FromInitialCreate_UpgradesWithEventsUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), $"personal-calendar-upgrade-{Guid.NewGuid():N}.db");
        await CreateVersion001DatabaseAsync(path);

        using var factory = ApiFactory.ForExistingDatabase(path);
        var client = factory.CreateClient();

        var timed = await GetAsync(client, TimedId);
        Assert.Equal("Dentist", timed.GetProperty("title").GetString());
        Assert.Equal("2026-10-14T09:00:00-05:00", timed.GetProperty("start").GetString());
        Assert.Equal(3, timed.GetProperty("version").GetInt32());
        AssertNotRecurring(timed);

        var allDay = await GetAsync(client, AllDayId);
        Assert.Equal("Vacation", allDay.GetProperty("title").GetString());
        Assert.Equal("2026-10-20", allDay.GetProperty("startDate").GetString());
        Assert.Equal("2026-10-23", allDay.GetProperty("endDate").GetString());
        AssertNotRecurring(allDay);
    }

    private static async Task CreateVersion001DatabaseAsync(string path)
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using var db = new CalendarDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20260929175058_InitialCreate");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Events (Id, Title, Location, Notes, IsAllDay, StartUtc, EndUtc, StartDate, EndDate, EntryTimeZone, CreatedUtc, UpdatedUtc, Version) VALUES " +
            $"('{TimedId}', 'Dentist', NULL, NULL, 0, '2026-10-14T14:00:00.000Z', '2026-10-14T15:00:00.000Z', NULL, NULL, 'America/Chicago', '2026-09-29T15:00:00.000Z', '2026-09-29T15:00:00.000Z', 3), " +
            $"('{AllDayId}', 'Vacation', NULL, NULL, 1, NULL, NULL, '2026-10-20', '2026-10-23', 'America/Chicago', '2026-09-29T15:00:00.000Z', '2026-09-29T15:00:00.000Z', 1)");
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string id)
    {
        var response = await client.GetAsync($"/api/events/{id}?timeZone={Chicago}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static void AssertNotRecurring(JsonElement details)
    {
        if (details.TryGetProperty("recurrence", out var recurrence)) Assert.Equal(JsonValueKind.Null, recurrence.ValueKind);
    }
}
