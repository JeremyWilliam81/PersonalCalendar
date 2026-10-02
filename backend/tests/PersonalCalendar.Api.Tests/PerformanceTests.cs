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
