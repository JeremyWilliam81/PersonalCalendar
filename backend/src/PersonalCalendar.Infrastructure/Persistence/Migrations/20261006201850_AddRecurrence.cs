using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCalendar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndLocal",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceRule",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceTimeZone",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesFirstDate",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesLastDate",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartLocal",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OccurrenceExceptions",
                columns: table => new
                {
                    SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalDate = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: true),
                    StartUtc = table.Column<string>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<string>(type: "TEXT", nullable: true),
                    EndDate = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccurrenceExceptions", x => new { x.SeriesId, x.OriginalDate });
                    table.CheckConstraint("CK_OccurrenceExceptions_Shape", "(IsDeleted = 1 AND Title IS NULL AND Location IS NULL AND Notes IS NULL AND IsAllDay IS NULL AND StartUtc IS NULL AND EndUtc IS NULL AND StartDate IS NULL AND EndDate IS NULL) OR (IsDeleted = 0 AND Title IS NOT NULL AND ((IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)))");
                    table.ForeignKey(
                        name: "FK_OccurrenceExceptions_Events_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Series",
                table: "Events",
                columns: new[] { "SeriesFirstDate", "SeriesLastDate" },
                filter: "RecurrenceRule IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_Recurrence",
                table: "Events",
                sql: "(RecurrenceRule IS NULL AND RecurrenceTimeZone IS NULL AND StartLocal IS NULL AND EndLocal IS NULL AND SeriesFirstDate IS NULL AND SeriesLastDate IS NULL) OR (RecurrenceRule IS NOT NULL AND RecurrenceTimeZone IS NOT NULL AND SeriesFirstDate IS NOT NULL AND ((IsAllDay = 0 AND StartLocal IS NOT NULL AND EndLocal IS NOT NULL) OR (IsAllDay = 1 AND StartLocal IS NULL AND EndLocal IS NULL)) AND (SeriesLastDate IS NULL OR SeriesLastDate >= SeriesFirstDate))");

            migrationBuilder.CreateIndex(
                name: "IX_OccurrenceExceptions_StartDate_EndDate",
                table: "OccurrenceExceptions",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OccurrenceExceptions_StartUtc_EndUtc",
                table: "OccurrenceExceptions",
                columns: new[] { "StartUtc", "EndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OccurrenceExceptions");

            migrationBuilder.DropIndex(
                name: "IX_Events_Series",
                table: "Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_Recurrence",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EndLocal",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RecurrenceRule",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RecurrenceTimeZone",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SeriesFirstDate",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SeriesLastDate",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "StartLocal",
                table: "Events");
        }
    }
}
