using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Tests;

public class CalendarEventTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 29, 15, 0);

    private static readonly EventSchedule Schedule =
        TimedSchedule.Create(Instant.FromUtc(2026, 10, 14, 14, 0), Instant.FromUtc(2026, 10, 14, 15, 0)).Value!;

    private static Validated<CalendarEvent> Create(string? title = "Dentist", string? location = null, string? notes = null) =>
        CalendarEvent.Create(title, location, notes, Schedule, Chicago, new FakeClock(Now));

    [Fact]
    public void Create_TrimsTitle()
    {
        var result = Create("  Dentist  ");

        Assert.Equal("Dentist", result.Value!.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespaceTitle_IsRequired(string? title)
    {
        var result = Create(title);

        Assert.Contains(result.Errors.Errors, e => e is { Field: "title", Code: ErrorCodes.TitleRequired });
    }

    [Fact]
    public void Create_TitleOf200Characters_IsAccepted() =>
        Assert.True(Create(new string('a', 200)).IsValid);

    [Fact]
    public void Create_TitleOf201Characters_IsTooLong() =>
        Assert.Contains(Create(new string('a', 201)).Errors.Errors, e => e is { Field: "title", Code: ErrorCodes.TitleTooLong });

    [Fact]
    public void Create_TrimsLocationAndTurnsWhitespaceIntoNull()
    {
        Assert.Equal("Main St", Create(location: "  Main St ").Value!.Location);
        Assert.Null(Create(location: "   ").Value!.Location);
    }

    [Fact]
    public void Create_LocationOf201Characters_IsTooLong() =>
        Assert.Contains(Create(location: new string('a', 201)).Errors.Errors, e => e is { Field: "location", Code: ErrorCodes.LocationTooLong });

    [Fact]
    public void Create_NotesOf5000Characters_IsAccepted() =>
        Assert.True(Create(notes: new string('a', 5000)).IsValid);

    [Fact]
    public void Create_NotesOf5001Characters_IsTooLong() =>
        Assert.Contains(Create(notes: new string('a', 5001)).Errors.Errors, e => e is { Field: "notes", Code: ErrorCodes.NotesTooLong });

    [Fact]
    public void Create_WithSeveralInvalidFields_ReportsEveryError()
    {
        var result = Create("", new string('a', 201), new string('a', 5001));

        Assert.Equal(3, result.Errors.Errors.Count);
    }

    [Fact]
    public void Update_Valid_ReplacesFieldsAndSchedule_IncrementsVersion_AndStampsUpdatedTime()
    {
        var clock = new FakeClock(Now);
        var calendarEvent = CalendarEvent.Create("Dentist", null, null, Schedule, Chicago, clock).Value!;
        var later = Now + Duration.FromHours(2);
        clock.Reset(later);
        var newSchedule = AllDaySchedule.Create(new LocalDate(2026, 10, 15), new LocalDate(2026, 10, 15)).Value!;
        var newYork = DateTimeZoneProviders.Tzdb["America/New_York"];

        var errors = calendarEvent.Update(" Dentist (moved) ", "Main St", "Bring card", newSchedule, newYork, clock);

        Assert.True(errors.IsValid);
        Assert.Equal("Dentist (moved)", calendarEvent.Title);
        Assert.Equal("Main St", calendarEvent.Location);
        Assert.Equal("Bring card", calendarEvent.Notes);
        Assert.Same(newSchedule, calendarEvent.Schedule);
        Assert.Equal("America/New_York", calendarEvent.EntryTimeZone);
        Assert.Equal(2, calendarEvent.Version);
        Assert.Equal(Now, calendarEvent.CreatedUtc);
        Assert.Equal(later, calendarEvent.UpdatedUtc);
    }

    [Fact]
    public void Update_Invalid_ReportsEveryError_AndLeavesEventUnchanged()
    {
        var calendarEvent = Create("Dentist", "Main St").Value!;

        var errors = calendarEvent.Update("", new string('a', 201), null, Schedule, Chicago, new FakeClock(Now + Duration.FromHours(1)));

        Assert.Equal(2, errors.Errors.Count);
        Assert.Equal("Dentist", calendarEvent.Title);
        Assert.Equal("Main St", calendarEvent.Location);
        Assert.Equal(1, calendarEvent.Version);
        Assert.Equal(Now, calendarEvent.UpdatedUtc);
    }

    [Fact]
    public void Create_SetsVersionTimestampsAndEntryZone()
    {
        var created = Create().Value!;

        Assert.Equal(1, created.Version);
        Assert.Equal(Now, created.CreatedUtc);
        Assert.Equal(Now, created.UpdatedUtc);
        Assert.Equal("America/Chicago", created.EntryTimeZone);
        Assert.Same(Schedule, created.Schedule);
    }
}
