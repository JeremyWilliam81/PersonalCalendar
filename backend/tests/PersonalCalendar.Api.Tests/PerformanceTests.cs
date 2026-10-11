using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using PersonalCalendar.Infrastructure.Persistence;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class PerformanceTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task MonthView_With5000Events_RespondsQuickly()
    {
        var client = _factory.CreateClient(); // starts the app and applies migrations
        await SeedAsync(5000);
        await client.GetMonthAsync(2026, 10); // warm-up

        var stopwatch = Stopwatch.StartNew();
        var month = await client.GetMonthAsync(2026, 10);
        stopwatch.Stop();

        Assert.NotEmpty(month.Days().SelectMany(d => d.GetProperty("events").EnumerateArray()));
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Month view took {stopwatch.ElapsedMilliseconds} ms (SC-004 budget: 500 ms).");
    }

    [Fact]
    public async Task DaysView_With5000Events_RespondsQuickly()
    {
        var client = _factory.CreateClient();
        await SeedAsync(5000);
        var url = $"/api/calendar/days?timeZone={Uri.EscapeDataString(Chicago)}&start=2026-10-11&count=7";
        (await client.GetAsync(url)).EnsureSuccessStatusCode(); // warm-up

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(url);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();
        var week = await response.ReadJsonAsync();
        Assert.NotEmpty(week.GetProperty("days").EnumerateArray().SelectMany(d => d.GetProperty("timed").EnumerateArray()));
        Assert.True(stopwatch.ElapsedMilliseconds <= 300, $"Week view took {stopwatch.ElapsedMilliseconds} ms (002 SC-002 budget: 300 ms).");
    }

    [Fact]
    public async Task Views_With5000EventsAnd200EndlessSeries_RespondWithin300Milliseconds()
    {
        var client = _factory.CreateClient();
        await SeedAsync(5000);
        await SeedSeriesAsync(client, 200);
        var urls = new[]
        {
            $"/api/calendar/month?timeZone={Uri.EscapeDataString(Chicago)}&year=2026&month=10",
            $"/api/calendar/days?timeZone={Uri.EscapeDataString(Chicago)}&start=2026-10-11&count=7",
        };

        foreach (var url in urls)
        {
            (await client.GetAsync(url)).EnsureSuccessStatusCode(); // warm-up
            var times = new List<long>();
            for (var run = 0; run < 5; run++)
            {
                var stopwatch = Stopwatch.StartNew();
                (await client.GetAsync(url)).EnsureSuccessStatusCode();
                times.Add(stopwatch.ElapsedMilliseconds);
            }

            var median = times.Order().ElementAt(2);
            Assert.True(median <= 300, $"{url} took a median of {median} ms (003 SC-004 budget: 300 ms).");
        }
    }

    /// <summary>A mix of rules that never end, all in America/Chicago (003 task T058).</summary>
    private static async Task SeedSeriesAsync(HttpClient client, int count)
    {
        object[] rules =
        [
            new { frequency = "daily", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)null, end = new { type = "never" } },
            new { frequency = "weekly", interval = 1, weekdays = new[] { "monday", "wednesday", "friday" }, monthly = (object?)null, end = new { type = "never" } },
            new { frequency = "monthly", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)new { type = "dayOfMonth" }, end = new { type = "never" } },
            new { frequency = "monthly", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)new { type = "weekdayPosition", ordinal = 2 }, end = new { type = "never" } },
            new { frequency = "yearly", interval = 1, weekdays = Array.Empty<string>(), monthly = (object?)null, end = new { type = "never" } },
        ];

        for (var i = 0; i < count; i++)
        {
            await client.CreateAsync(new
            {
                title = $"Series {i}",
                location = (string?)null,
                notes = (string?)null,
                isAllDay = false,
                timeZone = Chicago,
                start = "2025-01-08T09:00", // a Wednesday, the second of its month
                end = "2025-01-08T10:00",
                startDate = (string?)null,
                endDate = (string?)null,
                acceptAdjustedTimes = false,
                version = (int?)null,
                recurrence = rules[i % rules.Length],
            });
        }
    }

    /// <summary>Spreads events across 2026–2027: mostly one-hour timed events, every tenth an all-day event.</summary>
    private async Task SeedAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var first = new LocalDate(2026, 1, 1);
        var created = ApiFactory.Now;

        for (var i = 0; i < count; i++)
        {
            var day = first.PlusDays(i % 730);
            var row = new EventRow
            {
                Id = Guid.NewGuid(),
                Title = $"Event {i}",
                EntryTimeZone = Chicago,
                CreatedUtc = created,
                UpdatedUtc = created,
                Version = 1,
            };

            if (i % 10 == 0)
            {
                row.IsAllDay = true;
                row.StartDate = day;
                row.EndDate = day;
            }
            else
            {
                var start = day.At(new LocalTime(8 + i % 10, 0)).InUtc().ToInstant();
                row.StartUtc = start;
                row.EndUtc = start + Duration.FromHours(1);
            }

            db.Events.Add(row);
        }

        await db.SaveChangesAsync();
    }
}
