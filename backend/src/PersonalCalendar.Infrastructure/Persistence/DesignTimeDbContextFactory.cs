using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalCalendar.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to generate migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
