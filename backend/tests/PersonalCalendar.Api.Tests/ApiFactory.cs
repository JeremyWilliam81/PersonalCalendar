using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NodaTime.Testing;

namespace PersonalCalendar.Api.Tests;

/// <summary>Runs the API against a private temporary SQLite file with a fixed clock.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly Instant Now = Instant.FromUtc(2026, 9, 29, 15, 0);

    private readonly bool _ownsDatabase;

    public ApiFactory()
        : this(Path.Combine(Path.GetTempPath(), $"personal-calendar-test-{Guid.NewGuid():N}.db"), ownsDatabase: true)
    {
    }

    private ApiFactory(string databasePath, bool ownsDatabase)
    {
        DatabasePath = databasePath;
        _ownsDatabase = ownsDatabase;
    }

    public string DatabasePath { get; }

    public FakeClock Clock { get; } = new(Now);

    /// <summary>A new app instance on the same database file, simulating a restart (FR-013).</summary>
    public ApiFactory CreateRestartedFactory() => new(DatabasePath, ownsDatabase: false);

    /// <summary>An app on a database file prepared by the test, e.g. one created by an older migration. Deleted on dispose.</summary>
    public static ApiFactory ForExistingDatabase(string databasePath) => new(databasePath, ownsDatabase: true);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={DatabasePath};Pooling=False");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase && File.Exists(DatabasePath)) File.Delete(DatabasePath);
    }
}
