using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SettingsAndBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    SecondaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    LogoMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FaviconMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultAvatarMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoginHeadline = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LoginMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DashboardWelcome = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailFooter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Subdomain = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: true),
                    CustomDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Brands_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Brands_Media_DefaultAvatarMediaId",
                        column: x => x.DefaultAvatarMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Brands_Media_FaviconMediaId",
                        column: x => x.FaviconMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Brands_Media_LogoMediaId",
                        column: x => x.LogoMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoachSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SedentaryFactor = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: false),
                    LightlyActiveFactor = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: false),
                    ModeratelyActiveFactor = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: false),
                    VeryActiveFactor = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: false),
                    ExtraActiveFactor = table.Column<decimal>(type: "decimal(5,3)", precision: 5, scale: 3, nullable: false),
                    DeficitPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SurplusPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProteinGramsPerKg = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    FatGramsPerKg = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    DefaultMacroMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NutritionWeight = table.Column<int>(type: "int", nullable: false),
                    WorkoutWeight = table.Column<int>(type: "int", nullable: false),
                    CheckInWeight = table.Column<int>(type: "int", nullable: false),
                    StepsWeight = table.Column<int>(type: "int", nullable: false),
                    WaterWeight = table.Column<int>(type: "int", nullable: false),
                    SleepWeight = table.Column<int>(type: "int", nullable: false),
                    ReplacementRule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachSettings_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Units = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WhatsAppNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailSenderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_CoachId",
                table: "Brands",
                column: "CoachId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_CustomDomain",
                table: "Brands",
                column: "CustomDomain",
                unique: true,
                filter: "[CustomDomain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_DefaultAvatarMediaId",
                table: "Brands",
                column: "DefaultAvatarMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_FaviconMediaId",
                table: "Brands",
                column: "FaviconMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_LogoMediaId",
                table: "Brands",
                column: "LogoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Subdomain",
                table: "Brands",
                column: "Subdomain",
                unique: true,
                filter: "[Subdomain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSettings_CoachId",
                table: "CoachSettings",
                column: "CoachId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "CoachSettings");

            migrationBuilder.DropTable(
                name: "PlatformSettings");
        }
    }
}
