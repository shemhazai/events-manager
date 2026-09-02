using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventsUniqueNamePerYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Name",
                schema: "app",
                table: "Events");

            migrationBuilder.AddColumn<int>(
                name: "Year",
                schema: "app",
                table: "Events",
                type: "int",
                nullable: false,
                computedColumnSql: "YEAR([Date])",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_Name_Year",
                schema: "app",
                table: "Events",
                columns: new[] { "Name", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Name_Year",
                schema: "app",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Year",
                schema: "app",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Name",
                schema: "app",
                table: "Events",
                column: "Name",
                unique: true);
        }
    }
}
