using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ResumesOn",
                table: "Subscriptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SuspendedForOverdueInvoice",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OverdueSuspensionDays",
                table: "PlatformSettings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumesOn",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "SuspendedForOverdueInvoice",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "OverdueSuspensionDays",
                table: "PlatformSettings");
        }
    }
}
