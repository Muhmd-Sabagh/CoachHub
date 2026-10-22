using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Cleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormImportColumnMappings");

            migrationBuilder.DropTable(
                name: "FormImportProfiles");

            migrationBuilder.DropIndex(
                name: "IX_FormSubmissions_ImportFingerprint",
                table: "FormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ExternalSubmissionId",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "ImportFingerprint",
                table: "FormSubmissions");

            migrationBuilder.DropColumn(
                name: "ClientCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DietStatus",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "WorkoutStatus",
                table: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSubmissionId",
                table: "FormSubmissions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFingerprint",
                table: "FormSubmissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientCode",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DietStatus",
                table: "Clients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkoutStatus",
                table: "Clients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FormImportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientCodeHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalIdHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimestampHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormImportProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormImportProfiles_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormImportProfiles_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormImportColumnMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalColumnKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FormImportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Header = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionStableKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormImportColumnMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormImportColumnMappings_FormImportProfiles_FormImportProfileId",
                        column: x => x.FormImportProfileId,
                        principalTable: "FormImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ImportFingerprint",
                table: "FormSubmissions",
                column: "ImportFingerprint",
                unique: true,
                filter: "[ImportFingerprint] IS NOT NULL");

            // Restored rows need distinct codes before the unique index returns, and a valid workflow status.
            migrationBuilder.Sql(
                "UPDATE [Clients] SET [ClientCode] = UPPER(LEFT(REPLACE(CONVERT(nvarchar(36), [Id]), '-', ''), 12)), " +
                "[DietStatus] = N'NotStarted', [WorkoutStatus] = N'NotStarted';");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients",
                column: "ClientCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormImportColumnMappings_FormImportProfileId_ExternalColumnKey",
                table: "FormImportColumnMappings",
                columns: new[] { "FormImportProfileId", "ExternalColumnKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormImportColumnMappings_FormImportProfileId_QuestionStableKey",
                table: "FormImportColumnMappings",
                columns: new[] { "FormImportProfileId", "QuestionStableKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormImportProfiles_CoachId",
                table: "FormImportProfiles",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_FormImportProfiles_FormDefinitionId_Name",
                table: "FormImportProfiles",
                columns: new[] { "FormDefinitionId", "Name" },
                unique: true);
        }
    }
}
