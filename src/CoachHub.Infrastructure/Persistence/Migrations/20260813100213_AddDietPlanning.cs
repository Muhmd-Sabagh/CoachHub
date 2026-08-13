using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDietPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DietPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlans_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DietPlanNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanNotes_DietPlans_DietPlanId",
                        column: x => x.DietPlanId,
                        principalTable: "DietPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DietPlanVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActiveForPdf = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanVersions_DietPlans_DietPlanId",
                        column: x => x.DietPlanId,
                        principalTable: "DietPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DietPlanMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanMeals_DietPlanVersions_DietPlanVersionId",
                        column: x => x.DietPlanVersionId,
                        principalTable: "DietPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DietPlanMealFoods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanMealFoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanMealFoods_DietPlanMeals_MealId",
                        column: x => x.MealId,
                        principalTable: "DietPlanMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietPlanMealFoods_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietReplacementGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetMealFoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietReplacementGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietReplacementGroups_DietPlanMealFoods_TargetMealFoodItemId",
                        column: x => x.TargetMealFoodItemId,
                        principalTable: "DietPlanMealFoods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DietReplacementGroups_DietPlanMeals_TargetMealId",
                        column: x => x.TargetMealId,
                        principalTable: "DietPlanMeals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DietReplacementGroups_DietPlanVersions_DietPlanVersionId",
                        column: x => x.DietPlanVersionId,
                        principalTable: "DietPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DietReplacementOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietReplacementGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplacementFoodItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplacementMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietReplacementOptions", x => x.Id);
                    table.CheckConstraint("CK_DietReplacementOptions_OneTarget", "([ReplacementFoodItemId] IS NOT NULL AND [ReplacementMealId] IS NULL AND [Quantity] IS NOT NULL) OR ([ReplacementFoodItemId] IS NULL AND [ReplacementMealId] IS NOT NULL AND [Quantity] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DietReplacementOptions_DietPlanMeals_ReplacementMealId",
                        column: x => x.ReplacementMealId,
                        principalTable: "DietPlanMeals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DietReplacementOptions_DietReplacementGroups_DietReplacementGroupId",
                        column: x => x.DietReplacementGroupId,
                        principalTable: "DietReplacementGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietReplacementOptions_FoodItems_ReplacementFoodItemId",
                        column: x => x.ReplacementFoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealFoods_FoodItemId",
                table: "DietPlanMealFoods",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMealFoods_MealId_Order",
                table: "DietPlanMealFoods",
                columns: new[] { "MealId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanMeals_DietPlanVersionId_Order",
                table: "DietPlanMeals",
                columns: new[] { "DietPlanVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanNotes_DietPlanId_Order",
                table: "DietPlanNotes",
                columns: new[] { "DietPlanId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietPlans_ClientId",
                table: "DietPlans",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanVersions_DietPlanId_Order",
                table: "DietPlanVersions",
                columns: new[] { "DietPlanId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementGroups_DietPlanVersionId_Order",
                table: "DietReplacementGroups",
                columns: new[] { "DietPlanVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementGroups_TargetMealFoodItemId",
                table: "DietReplacementGroups",
                column: "TargetMealFoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementGroups_TargetMealId",
                table: "DietReplacementGroups",
                column: "TargetMealId");

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementOptions_DietReplacementGroupId_Order",
                table: "DietReplacementOptions",
                columns: new[] { "DietReplacementGroupId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementOptions_ReplacementFoodItemId",
                table: "DietReplacementOptions",
                column: "ReplacementFoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DietReplacementOptions_ReplacementMealId",
                table: "DietReplacementOptions",
                column: "ReplacementMealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DietPlanNotes");

            migrationBuilder.DropTable(
                name: "DietReplacementOptions");

            migrationBuilder.DropTable(
                name: "DietReplacementGroups");

            migrationBuilder.DropTable(
                name: "DietPlanMealFoods");

            migrationBuilder.DropTable(
                name: "DietPlanMeals");

            migrationBuilder.DropTable(
                name: "DietPlanVersions");

            migrationBuilder.DropTable(
                name: "DietPlans");
        }
    }
}
