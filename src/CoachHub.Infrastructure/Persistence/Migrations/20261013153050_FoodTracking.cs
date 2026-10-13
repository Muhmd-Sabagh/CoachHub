using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P8: meal options (§13), exact or flexible alternatives with units (§7), and the client's food diary (§8).
    /// Existing alternative groups become Exact: their quantities were always set by the coach.
    /// </summary>
    public partial class FoodTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DietReplacementOptions_OneTarget",
                table: "DietReplacementOptions");

            migrationBuilder.AddColumn<Guid>(
                name: "FoodUnitId",
                table: "DietReplacementOptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "DietReplacementGroups",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Exact");

            migrationBuilder.AddColumn<Guid>(
                name: "MealOptionId",
                table: "DietPlanMealFoods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DietPlanMealOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanMealOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanMealOptions_DietPlanMeals_MealId",
                        column: x => x.MealId,
                        principalTable: "DietPlanMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FoodUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnitLabelAr = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Calories = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Protein = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Carbohydrates = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Fat = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    Fiber = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedMealFoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplacementOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoggedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodLogEntries", x => x.Id);
                    table.CheckConstraint("CK_FoodLogEntries_Nutrients", "[Calories] >= 0 AND [Protein] >= 0 AND [Carbohydrates] >= 0 AND [Fat] >= 0 AND [Fiber] >= 0");
                    table.CheckConstraint("CK_FoodLogEntries_Portion", "([FoodItemId] IS NULL AND [Quantity] IS NULL AND [BaseAmount] IS NULL AND [FoodUnitId] IS NULL) OR ([FoodItemId] IS NOT NULL AND [Quantity] > 0 AND [BaseAmount] > 0 AND [UnitLabel] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FoodLogEntries_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodLogEntries_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLogEntries_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodLogEntries_FoodUnits_FoodUnitId",
                        column: x => x.FoodUnitId,
                        principalTable: "FoodUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NutritionDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionDays_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NutritionDays_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NutritionDayMealChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NutritionDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionDayMealChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionDayMealChoices_NutritionDays_NutritionDayId",
                        column: x => x.NutritionDayId,
                        principalTable: "NutritionDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementOptions_FoodUnitId",
                table: "DietReplacementOptions",
                column: "FoodUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DietReplacementOptions_OneTarget",
                table: "DietReplacementOptions",
                sql: "([ReplacementFoodItemId] IS NOT NULL AND [ReplacementMealId] IS NULL) OR ([ReplacementFoodItemId] IS NULL AND [ReplacementMealId] IS NOT NULL AND [Quantity] IS NULL AND [FoodUnitId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealFoods_MealOptionId",
                table: "DietPlanMealFoods",
                column: "MealOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealOptions_MealId_Order",
                table: "DietPlanMealOptions",
                columns: new[] { "MealId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealOptions_RevisionId",
                table: "DietPlanMealOptions",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLogEntries_ClientId_Date",
                table: "FoodLogEntries",
                columns: new[] { "ClientId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FoodLogEntries_CoachId",
                table: "FoodLogEntries",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLogEntries_FoodItemId",
                table: "FoodLogEntries",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodLogEntries_FoodUnitId",
                table: "FoodLogEntries",
                column: "FoodUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionDayMealChoices_NutritionDayId_MealId",
                table: "NutritionDayMealChoices",
                columns: new[] { "NutritionDayId", "MealId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NutritionDays_ClientId_Date",
                table: "NutritionDays",
                columns: new[] { "ClientId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NutritionDays_CoachId",
                table: "NutritionDays",
                column: "CoachId");

            migrationBuilder.AddForeignKey(
                name: "FK_DietPlanMealFoods_DietPlanMealOptions_MealOptionId",
                table: "DietPlanMealFoods",
                column: "MealOptionId",
                principalTable: "DietPlanMealOptions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DietReplacementOptions_FoodUnits_FoodUnitId",
                table: "DietReplacementOptions",
                column: "FoodUnitId",
                principalTable: "FoodUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DietPlanMealFoods_DietPlanMealOptions_MealOptionId",
                table: "DietPlanMealFoods");

            migrationBuilder.DropForeignKey(
                name: "FK_DietReplacementOptions_FoodUnits_FoodUnitId",
                table: "DietReplacementOptions");

            migrationBuilder.DropTable(
                name: "DietPlanMealOptions");

            migrationBuilder.DropTable(
                name: "FoodLogEntries");

            migrationBuilder.DropTable(
                name: "NutritionDayMealChoices");

            migrationBuilder.DropTable(
                name: "NutritionDays");

            migrationBuilder.DropIndex(
                name: "IX_DietReplacementOptions_FoodUnitId",
                table: "DietReplacementOptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DietReplacementOptions_OneTarget",
                table: "DietReplacementOptions");

            migrationBuilder.DropIndex(
                name: "IX_DietPlanMealFoods_MealOptionId",
                table: "DietPlanMealFoods");

            // The old schema has no calculated quantities or meal options: calculated alternatives are removed, and
            // the foods of every option of a meal become plain foods of that meal.
            migrationBuilder.Sql(
                "DELETE FROM [DietReplacementOptions] WHERE [ReplacementFoodItemId] IS NOT NULL AND [Quantity] IS NULL;");

            migrationBuilder.DropColumn(
                name: "FoodUnitId",
                table: "DietReplacementOptions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "DietReplacementGroups");

            migrationBuilder.DropColumn(
                name: "MealOptionId",
                table: "DietPlanMealFoods");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DietReplacementOptions_OneTarget",
                table: "DietReplacementOptions",
                sql: "([ReplacementFoodItemId] IS NOT NULL AND [ReplacementMealId] IS NULL AND [Quantity] IS NOT NULL) OR ([ReplacementFoodItemId] IS NULL AND [ReplacementMealId] IS NOT NULL AND [Quantity] IS NULL)");
        }
    }
}
