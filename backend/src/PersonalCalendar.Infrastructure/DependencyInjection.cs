using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Infrastructure.Persistence;

namespace PersonalCalendar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        EnsureDatabaseFolderExists(connectionString);

        services.AddDbContext<CalendarDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IEventRepository, EfEventRepository>();
        return services;
    }

    private static void EnsureDatabaseFolderExists(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var folder = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
    }
}
