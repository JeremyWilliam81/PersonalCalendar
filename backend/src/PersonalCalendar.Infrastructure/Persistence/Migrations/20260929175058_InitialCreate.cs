using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCalendar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartUtc = table.Column<string>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<string>(type: "TEXT", nullable: true),
                    EndDate = table.Column<string>(type: "TEXT", nullable: true),
                    EntryTimeZone = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_Schedule", "(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartDate_EndDate",
                table: "Events",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartUtc_EndUtc",
                table: "Events",
                columns: new[] { "StartUtc", "EndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
