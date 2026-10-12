using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientProfileAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_FormCode",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "FormCode",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "FormCodeHeader",
                table: "FormImportProfiles",
                newName: "ClientCodeHeader");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccountMessage",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StatusOverride",
                table: "Clients",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StatusOverrideUntil",
                table: "Clients",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HeightCm = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    StartingWeightKg = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    StartingBodyFatPercent = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    Goal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ActivityLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TrainingDaysPerWeek = table.Column<int>(type: "int", nullable: true),
                    AverageDailySteps = table.Column<int>(type: "int", nullable: true),
                    TrainingLocation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TrainingPreferences = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FoodPreferences = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreferredFoodsNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DislikedFoodsNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DietaryRestrictions = table.Column<int>(type: "int", nullable: false),
                    DietaryNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreferredMealCount = table.Column<int>(type: "int", nullable: true),
                    Bedtime = table.Column<TimeOnly>(type: "time", nullable: true),
                    WakeTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    LifestyleNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProfiles_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientFoodPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientFoodPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientFoodPreferences_ClientProfiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "ClientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientFoodPreferences_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEquipment_ClientProfiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "ClientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientEquipment_ClientProfileId_EquipmentId",
                table: "ClientEquipment",
                columns: new[] { "ClientProfileId", "EquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientEquipment_EquipmentId",
                table: "ClientEquipment",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientFoodPreferences_ClientProfileId_FoodItemId",
                table: "ClientFoodPreferences",
                columns: new[] { "ClientProfileId", "FoodItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientFoodPreferences_FoodItemId",
                table: "ClientFoodPreferences",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_ClientId",
                table: "ClientProfiles",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_NameEn",
                table: "Equipment",
                column: "NameEn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientEquipment");

            migrationBuilder.DropTable(
                name: "ClientFoodPreferences");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "IsAccountMessage",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "StatusOverride",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "StatusOverrideUntil",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "ClientCodeHeader",
                table: "FormImportProfiles",
                newName: "FormCodeHeader");

            migrationBuilder.AddColumn<string>(
                name: "FormCode",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_FormCode",
                table: "Clients",
                column: "FormCode",
                unique: true);
        }
    }
}
