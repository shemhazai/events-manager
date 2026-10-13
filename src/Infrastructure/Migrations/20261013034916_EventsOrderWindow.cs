using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventsOrderWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "OrderWindowEndDate",
                schema: "app",
                table: "Events",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OrderWindowStartDate",
                schema: "app",
                table: "Events",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_OrderWindow_BothOrNeither",
                schema: "app",
                table: "Events",
                sql: "([OrderWindowStartDate] IS NULL AND [OrderWindowEndDate] IS NULL) OR ([OrderWindowStartDate] IS NOT NULL AND [OrderWindowEndDate] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_OrderWindow_Order",
                schema: "app",
                table: "Events",
                sql: "[OrderWindowStartDate] IS NULL OR ([OrderWindowStartDate] <= [OrderWindowEndDate] AND [OrderWindowEndDate] <= [Date])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_OrderWindow_BothOrNeither",
                schema: "app",
                table: "Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_OrderWindow_Order",
                schema: "app",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrderWindowEndDate",
                schema: "app",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrderWindowStartDate",
                schema: "app",
                table: "Events");
        }
    }
}
