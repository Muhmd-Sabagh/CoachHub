using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NutritionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columns are added first so existing data can be converted before the free-text unit goes.
            migrationBuilder.AddColumn<string>(
                name: "BaseUnit",
                table: "FoodItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CoachId",
                table: "FoodItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FiberPer100",
                table: "FoodItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PieceNameAr",
                table: "FoodItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PieceNameEn",
                table: "FoodItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServingSize",
                table: "FoodItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SodiumMgPer100",
                table: "FoodItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SugarPer100",
                table: "FoodItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetCalories",
                table: "DietPlanRevisions",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetCarbohydrates",
                table: "DietPlanRevisions",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetFat",
                table: "DietPlanRevisions",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetMealCount",
                table: "DietPlanRevisions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetProtein",
                table: "DietPlanRevisions",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetSnackCount",
                table: "DietPlanRevisions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "DietPlanMeals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FoodUnitId",
                table: "DietPlanMealFoods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroCarbohydratePercent",
                table: "ClientProfiles",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroFatGrams",
                table: "ClientProfiles",
                type: "decimal(6,1)",
                precision: 6,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroFatGramsPerKg",
                table: "ClientProfiles",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroFatPercent",
                table: "ClientProfiles",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MacroMode",
                table: "ClientProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroProteinGrams",
                table: "ClientProfiles",
                type: "decimal(6,1)",
                precision: 6,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroProteinGramsPerKg",
                table: "ClientProfiles",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MacroProteinPercent",
                table: "ClientProfiles",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FoodUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodUnits_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // D3: legacy foods were "per 100 of the stated unit". Grams and millilitres become base units;
            // any other unit (slice, scoop, large egg ...) becomes a piece, keeping its name (LegacyFoodUnits).
            migrationBuilder.Sql("""
                UPDATE [FoodItems] SET
                    [BaseUnit] = CASE
                        WHEN LTRIM(RTRIM([MeasurementUnit])) IN (N'', N'gram', N'grams', N'g') THEN N'Gram'
                        WHEN LTRIM(RTRIM([MeasurementUnit])) IN (N'ml', N'مل') THEN N'Milliliter'
                        ELSE N'Piece' END,
                    [PieceNameEn] = CASE LTRIM(RTRIM([MeasurementUnit]))
                        WHEN N'gram' THEN NULL WHEN N'grams' THEN NULL WHEN N'g' THEN NULL WHEN N'' THEN NULL
                        WHEN N'ml' THEN NULL WHEN N'مل' THEN NULL
                        WHEN N'شريحة' THEN N'slice' WHEN N'كبيره الحجم' THEN N'large' WHEN N'سكوب' THEN N'scoop'
                        WHEN N'كوب' THEN N'cup' WHEN N'معلقه كبيره' THEN N'tablespoon' WHEN N'رشه' THEN N'spray'
                        ELSE N'piece' END,
                    [PieceNameAr] = CASE
                        WHEN LTRIM(RTRIM([MeasurementUnit])) IN (N'', N'gram', N'grams', N'g', N'ml', N'مل') THEN NULL
                        WHEN [MeasurementUnit] LIKE N'%[ء-ي]%' THEN LEFT(LTRIM(RTRIM([MeasurementUnit])), 50)
                        ELSE NULL END;
                UPDATE [FoodItems] SET [ServingSize] = CASE WHEN [BaseUnit] = N'Piece' THEN 1 ELSE 100 END;
                UPDATE [DietPlanMeals] SET [Type] = CASE
                    WHEN LOWER([NameEn]) LIKE N'%breakfast%' OR [NameAr] LIKE N'%فطور%' THEN N'Breakfast'
                    WHEN LOWER([NameEn]) LIKE N'%lunch%' OR [NameAr] LIKE N'%غدا%' THEN N'Lunch'
                    WHEN LOWER([NameEn]) LIKE N'%dinner%' OR LOWER([NameEn]) LIKE N'%supper%' OR [NameAr] LIKE N'%عشاء%' THEN N'Dinner'
                    ELSE N'Snack' END;
                """);
            migrationBuilder.DropColumn(
                name: "MeasurementUnit",
                table: "FoodItems");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_CoachId",
                table: "FoodItems",
                column: "CoachId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FoodItems_PieceName",
                table: "FoodItems",
                sql: "[BaseUnit] <> 'Piece' OR [PieceNameEn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealFoods_FoodUnitId",
                table: "DietPlanMealFoods",
                column: "FoodUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodUnits_FoodItemId_Order",
                table: "FoodUnits",
                columns: new[] { "FoodItemId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_DietPlanMealFoods_FoodUnits_FoodUnitId",
                table: "DietPlanMealFoods",
                column: "FoodUnitId",
                principalTable: "FoodUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FoodItems_Coaches_CoachId",
                table: "FoodItems",
                column: "CoachId",
                principalTable: "Coaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeasurementUnit",
                table: "FoodItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("""
                UPDATE [FoodItems] SET [MeasurementUnit] = CASE [BaseUnit]
                    WHEN N'Gram' THEN N'gram' WHEN N'Milliliter' THEN N'ml'
                    ELSE COALESCE([PieceNameAr], [PieceNameEn], N'piece') END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DietPlanMealFoods_FoodUnits_FoodUnitId",
                table: "DietPlanMealFoods");

            migrationBuilder.DropForeignKey(
                name: "FK_FoodItems_Coaches_CoachId",
                table: "FoodItems");

            migrationBuilder.DropTable(
                name: "FoodUnits");

            migrationBuilder.DropIndex(
                name: "IX_FoodItems_CoachId",
                table: "FoodItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FoodItems_PieceName",
                table: "FoodItems");

            migrationBuilder.DropIndex(
                name: "IX_DietPlanMealFoods_FoodUnitId",
                table: "DietPlanMealFoods");

            migrationBuilder.DropColumn(
                name: "BaseUnit",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "CoachId",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "FiberPer100",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "PieceNameAr",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "PieceNameEn",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "ServingSize",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "SodiumMgPer100",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "SugarPer100",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "TargetCalories",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "TargetCarbohydrates",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "TargetFat",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "TargetMealCount",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "TargetProtein",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "TargetSnackCount",
                table: "DietPlanRevisions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "DietPlanMeals");

            migrationBuilder.DropColumn(
                name: "FoodUnitId",
                table: "DietPlanMealFoods");

            migrationBuilder.DropColumn(
                name: "MacroCarbohydratePercent",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroFatGrams",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroFatGramsPerKg",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroFatPercent",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroMode",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroProteinGrams",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroProteinGramsPerKg",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "MacroProteinPercent",
                table: "ClientProfiles");

        }
    }
}
