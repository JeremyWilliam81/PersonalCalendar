using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using PersonalCalendar.Api.Endpoints;
using PersonalCalendar.Api.Http;
using PersonalCalendar.Application;
using PersonalCalendar.Infrastructure;
using PersonalCalendar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Calendar") ?? DefaultConnectionString();

builder.Services
    .AddApplication()
    .AddInfrastructure(connectionString)
    .AddSingleton<IClock>(SystemClock.Instance)
    .AddSingleton<IDateTimeZoneProvider>(DateTimeZoneProviders.Tzdb)
    .ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb))
    .AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(ProblemResults.HandleUnexpectedAsync));

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CalendarDbContext>().Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapEventEndpoints();
app.MapCalendarEndpoints();
app.MapGroup("/api").MapFallback(() => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

static string DefaultConnectionString()
{
    var folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalCalendar");
    return $"Data Source={Path.Combine(folder, "calendar.db")}";
}

public partial class Program;
