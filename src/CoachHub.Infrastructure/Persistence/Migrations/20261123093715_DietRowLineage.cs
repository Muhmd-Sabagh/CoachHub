using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DietRowLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LineageId",
                table: "DietPlanVariants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LineageId",
                table: "DietPlanMeals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LineageId",
                table: "DietPlanMealOptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing rows start their own lineage (P22); earlier versions are not linked after the fact.
            migrationBuilder.Sql("UPDATE [DietPlanVariants] SET [LineageId] = [Id];");
            migrationBuilder.Sql("UPDATE [DietPlanMeals] SET [LineageId] = [Id];");
            migrationBuilder.Sql("UPDATE [DietPlanMealOptions] SET [LineageId] = [Id];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineageId",
                table: "DietPlanVariants");

            migrationBuilder.DropColumn(
                name: "LineageId",
                table: "DietPlanMeals");

            migrationBuilder.DropColumn(
                name: "LineageId",
                table: "DietPlanMealOptions");
        }
    }
}
