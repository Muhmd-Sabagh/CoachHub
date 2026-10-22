using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAt",
                table: "Subscriptions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Subscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DurationPresetList",
                table: "PlatformSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SubscriptionAccessRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlatformSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Access = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionAccessRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionAccessRules_PlatformSettings_PlatformSettingsId",
                        column: x => x.PlatformSettingsId,
                        principalTable: "PlatformSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAccessRules_PlatformSettingsId_Status_Feature",
                table: "SubscriptionAccessRules",
                columns: new[] { "PlatformSettingsId", "Status", "Feature" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionAccessRules");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "DurationPresetList",
                table: "PlatformSettings");
        }
    }
}
