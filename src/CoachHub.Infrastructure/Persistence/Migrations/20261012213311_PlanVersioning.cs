using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P6 (D4): plans gain version history. Hand-written so existing data survives: every plan becomes
    /// version 1 (Active), the old "versions" table becomes the variants table, and every content row
    /// learns its version. Deliveries keep their snapshot and point at version 1 where the plan still exists.
    /// </summary>
    public partial class PlanVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Foreign keys that point at the old structure.
            migrationBuilder.DropForeignKey(name: "FK_DietPlanMeals_DietPlanVersions_DietPlanVersionId", table: "DietPlanMeals");
            migrationBuilder.DropForeignKey(name: "FK_DietReplacementGroups_DietPlanVersions_DietPlanVersionId", table: "DietReplacementGroups");
            migrationBuilder.DropForeignKey(name: "FK_DietPlanVersions_DietPlans_DietPlanId", table: "DietPlanVersions");
            migrationBuilder.DropForeignKey(name: "FK_DietPlanNotes_DietPlans_DietPlanId", table: "DietPlanNotes");
            migrationBuilder.DropForeignKey(name: "FK_WorkoutDays_WorkoutPlans_WorkoutPlanId", table: "WorkoutDays");
            migrationBuilder.DropForeignKey(name: "FK_WorkoutPlanNotes_WorkoutPlans_WorkoutPlanId", table: "WorkoutPlanNotes");
            migrationBuilder.DropIndex(name: "IX_DietPlanVersions_DietPlanId_Order", table: "DietPlanVersions");
            migrationBuilder.DropIndex(name: "IX_DietPlanMeals_DietPlanVersionId_Order", table: "DietPlanMeals");
            migrationBuilder.DropIndex(name: "IX_DietReplacementGroups_DietPlanVersionId_Order", table: "DietReplacementGroups");

            // Version tables; every existing plan becomes version 1.
            CreateRevisionTable(migrationBuilder, "DietPlanRevisions", "DietPlans");
            CreateRevisionTable(migrationBuilder, "WorkoutPlanRevisions", "WorkoutPlans");
            migrationBuilder.Sql("""
                INSERT INTO [DietPlanRevisions] ([Id], [PlanId], [VersionNumber], [Status], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), [Id], 1, N'Active', [CreatedAt], [CreatedAt] FROM [DietPlans];
                INSERT INTO [WorkoutPlanRevisions] ([Id], [PlanId], [VersionNumber], [Status], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), [Id], 1, N'Active', [CreatedAt], [CreatedAt] FROM [WorkoutPlans];
                """);

            // The old "versions" are the variants (ordered tabs) of version 1.
            migrationBuilder.DropPrimaryKey(name: "PK_DietPlanVersions", table: "DietPlanVersions");
            migrationBuilder.RenameTable(name: "DietPlanVersions", newName: "DietPlanVariants");
            migrationBuilder.RenameColumn(name: "DietPlanId", table: "DietPlanVariants", newName: "RevisionId");
            migrationBuilder.AddPrimaryKey(name: "PK_DietPlanVariants", table: "DietPlanVariants", column: "Id");
            migrationBuilder.Sql("""
                UPDATE v SET [RevisionId] = r.[Id]
                FROM [DietPlanVariants] v JOIN [DietPlanRevisions] r ON r.[PlanId] = v.[RevisionId];
                """);

            // Diet content: variant links keep their values; every row records its version.
            migrationBuilder.RenameColumn(name: "DietPlanVersionId", table: "DietPlanMeals", newName: "DietPlanVariantId");
            migrationBuilder.RenameColumn(name: "DietPlanVersionId", table: "DietReplacementGroups", newName: "DietPlanVariantId");
            AddRevisionColumn(migrationBuilder, "DietPlanMeals");
            AddRevisionColumn(migrationBuilder, "DietReplacementGroups");
            AddRevisionColumn(migrationBuilder, "DietPlanMealFoods");
            AddRevisionColumn(migrationBuilder, "DietReplacementOptions");
            migrationBuilder.RenameColumn(name: "DietPlanId", table: "DietPlanNotes", newName: "RevisionId");
            migrationBuilder.RenameIndex(name: "IX_DietPlanNotes_DietPlanId_Order", table: "DietPlanNotes", newName: "IX_DietPlanNotes_RevisionId_Order");
            migrationBuilder.Sql("""
                UPDATE m SET [RevisionId] = v.[RevisionId]
                FROM [DietPlanMeals] m JOIN [DietPlanVariants] v ON v.[Id] = m.[DietPlanVariantId];
                UPDATE g SET [RevisionId] = v.[RevisionId]
                FROM [DietReplacementGroups] g JOIN [DietPlanVariants] v ON v.[Id] = g.[DietPlanVariantId];
                UPDATE f SET [RevisionId] = m.[RevisionId]
                FROM [DietPlanMealFoods] f JOIN [DietPlanMeals] m ON m.[Id] = f.[MealId];
                UPDATE o SET [RevisionId] = g.[RevisionId]
                FROM [DietReplacementOptions] o JOIN [DietReplacementGroups] g ON g.[Id] = o.[DietReplacementGroupId];
                UPDATE n SET [RevisionId] = r.[Id]
                FROM [DietPlanNotes] n JOIN [DietPlanRevisions] r ON r.[PlanId] = n.[RevisionId];
                """);

            // Workout content.
            migrationBuilder.RenameColumn(name: "WorkoutPlanId", table: "WorkoutDays", newName: "RevisionId");
            migrationBuilder.RenameIndex(name: "IX_WorkoutDays_WorkoutPlanId_Order", table: "WorkoutDays", newName: "IX_WorkoutDays_RevisionId_Order");
            migrationBuilder.RenameColumn(name: "WorkoutPlanId", table: "WorkoutPlanNotes", newName: "RevisionId");
            migrationBuilder.RenameIndex(name: "IX_WorkoutPlanNotes_WorkoutPlanId_Order", table: "WorkoutPlanNotes", newName: "IX_WorkoutPlanNotes_RevisionId_Order");
            AddRevisionColumn(migrationBuilder, "WorkoutExercises");
            migrationBuilder.Sql("""
                UPDATE d SET [RevisionId] = r.[Id]
                FROM [WorkoutDays] d JOIN [WorkoutPlanRevisions] r ON r.[PlanId] = d.[RevisionId];
                UPDATE n SET [RevisionId] = r.[Id]
                FROM [WorkoutPlanNotes] n JOIN [WorkoutPlanRevisions] r ON r.[PlanId] = n.[RevisionId];
                UPDATE e SET [RevisionId] = d.[RevisionId]
                FROM [WorkoutExercises] e JOIN [WorkoutDays] d ON d.[Id] = e.[WorkoutDayId];
                """);

            // Deliveries name the delivered version; a diet's old "version" was its variant.
            migrationBuilder.AddColumn<Guid>(name: "RevisionId", table: "DeliveredPlans", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<int>(name: "RevisionNumber", table: "DeliveredPlans", type: "int", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "VariantId", table: "DeliveredPlans", type: "uniqueidentifier", nullable: true);
            migrationBuilder.Sql("""
                UPDATE p SET [VariantId] = CASE WHEN p.[PlanType] = N'Diet' THEN p.[VersionId] END,
                    [RevisionId] = COALESCE(d.[Id], w.[Id]),
                    [RevisionNumber] = CASE WHEN COALESCE(d.[Id], w.[Id]) IS NOT NULL THEN 1 END
                FROM [DeliveredPlans] p
                LEFT JOIN [DietPlanRevisions] d ON p.[PlanType] = N'Diet' AND d.[PlanId] = p.[PlanId]
                LEFT JOIN [WorkoutPlanRevisions] w ON p.[PlanType] = N'Workout' AND w.[PlanId] = p.[PlanId];
                """);
            migrationBuilder.DropColumn(name: "VersionId", table: "DeliveredPlans");

            // Keys and indexes of the new structure.
            CreateRevisionIndexes(migrationBuilder, "DietPlanRevisions");
            CreateRevisionIndexes(migrationBuilder, "WorkoutPlanRevisions");
            migrationBuilder.CreateIndex(name: "IX_DietPlanVariants_RevisionId_Order", table: "DietPlanVariants",
                columns: new[] { "RevisionId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_DietPlanMeals_DietPlanVariantId_Order", table: "DietPlanMeals",
                columns: new[] { "DietPlanVariantId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_DietReplacementGroups_DietPlanVariantId_Order", table: "DietReplacementGroups",
                columns: new[] { "DietPlanVariantId", "Order" }, unique: true);
            foreach (var table in new[] { "DietPlanMeals", "DietReplacementGroups", "DietPlanMealFoods", "DietReplacementOptions", "WorkoutExercises" })
                migrationBuilder.CreateIndex(name: $"IX_{table}_RevisionId", table: table, column: "RevisionId");

            AddCascade(migrationBuilder, "DietPlanVariants", "RevisionId", "DietPlanRevisions");
            AddCascade(migrationBuilder, "DietPlanMeals", "DietPlanVariantId", "DietPlanVariants");
            AddCascade(migrationBuilder, "DietReplacementGroups", "DietPlanVariantId", "DietPlanVariants");
            AddCascade(migrationBuilder, "DietPlanNotes", "RevisionId", "DietPlanRevisions");
            AddCascade(migrationBuilder, "WorkoutDays", "RevisionId", "WorkoutPlanRevisions");
            AddCascade(migrationBuilder, "WorkoutPlanNotes", "RevisionId", "WorkoutPlanRevisions");
        }

        /// <inheritdoc />
        /// <remarks>Reverts to one content set per plan: only the current versions survive.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_DietPlanVariants_DietPlanRevisions_RevisionId", table: "DietPlanVariants");
            migrationBuilder.DropForeignKey(name: "FK_DietPlanMeals_DietPlanVariants_DietPlanVariantId", table: "DietPlanMeals");
            migrationBuilder.DropForeignKey(name: "FK_DietReplacementGroups_DietPlanVariants_DietPlanVariantId", table: "DietReplacementGroups");
            migrationBuilder.DropForeignKey(name: "FK_DietPlanNotes_DietPlanRevisions_RevisionId", table: "DietPlanNotes");
            migrationBuilder.DropForeignKey(name: "FK_WorkoutDays_WorkoutPlanRevisions_RevisionId", table: "WorkoutDays");
            migrationBuilder.DropForeignKey(name: "FK_WorkoutPlanNotes_WorkoutPlanRevisions_RevisionId", table: "WorkoutPlanNotes");
            migrationBuilder.DropIndex(name: "IX_DietPlanVariants_RevisionId_Order", table: "DietPlanVariants");
            migrationBuilder.DropIndex(name: "IX_DietPlanMeals_DietPlanVariantId_Order", table: "DietPlanMeals");
            migrationBuilder.DropIndex(name: "IX_DietReplacementGroups_DietPlanVariantId_Order", table: "DietReplacementGroups");
            foreach (var table in new[] { "DietPlanMeals", "DietReplacementGroups", "DietPlanMealFoods", "DietReplacementOptions", "WorkoutExercises" })
                migrationBuilder.DropIndex(name: $"IX_{table}_RevisionId", table: table);

            // Archived history has no place in the old model.
            migrationBuilder.Sql("""
                DELETE FROM [DietReplacementOptions] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [DietReplacementGroups] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [DietPlanMealFoods] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [DietPlanMeals] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [DietPlanVariants] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [DietPlanNotes] WHERE [RevisionId] IN (SELECT [Id] FROM [DietPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [WorkoutExercises] WHERE [RevisionId] IN (SELECT [Id] FROM [WorkoutPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [WorkoutDays] WHERE [RevisionId] IN (SELECT [Id] FROM [WorkoutPlanRevisions] WHERE [Status] = N'Archived');
                DELETE FROM [WorkoutPlanNotes] WHERE [RevisionId] IN (SELECT [Id] FROM [WorkoutPlanRevisions] WHERE [Status] = N'Archived');
                UPDATE v SET [RevisionId] = r.[PlanId] FROM [DietPlanVariants] v JOIN [DietPlanRevisions] r ON r.[Id] = v.[RevisionId];
                UPDATE n SET [RevisionId] = r.[PlanId] FROM [DietPlanNotes] n JOIN [DietPlanRevisions] r ON r.[Id] = n.[RevisionId];
                UPDATE d SET [RevisionId] = r.[PlanId] FROM [WorkoutDays] d JOIN [WorkoutPlanRevisions] r ON r.[Id] = d.[RevisionId];
                UPDATE n SET [RevisionId] = r.[PlanId] FROM [WorkoutPlanNotes] n JOIN [WorkoutPlanRevisions] r ON r.[Id] = n.[RevisionId];
                """);

            migrationBuilder.AddColumn<Guid>(name: "VersionId", table: "DeliveredPlans", type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("UPDATE [DeliveredPlans] SET [VersionId] = COALESCE([VariantId], [PlanId]);");
            migrationBuilder.DropColumn(name: "RevisionId", table: "DeliveredPlans");
            migrationBuilder.DropColumn(name: "RevisionNumber", table: "DeliveredPlans");
            migrationBuilder.DropColumn(name: "VariantId", table: "DeliveredPlans");

            foreach (var table in new[] { "DietPlanMeals", "DietReplacementGroups", "DietPlanMealFoods", "DietReplacementOptions", "WorkoutExercises" })
                migrationBuilder.DropColumn(name: "RevisionId", table: table);
            migrationBuilder.RenameColumn(name: "DietPlanVariantId", table: "DietPlanMeals", newName: "DietPlanVersionId");
            migrationBuilder.RenameColumn(name: "DietPlanVariantId", table: "DietReplacementGroups", newName: "DietPlanVersionId");
            migrationBuilder.RenameColumn(name: "RevisionId", table: "DietPlanNotes", newName: "DietPlanId");
            migrationBuilder.RenameIndex(name: "IX_DietPlanNotes_RevisionId_Order", table: "DietPlanNotes", newName: "IX_DietPlanNotes_DietPlanId_Order");
            migrationBuilder.RenameColumn(name: "RevisionId", table: "WorkoutDays", newName: "WorkoutPlanId");
            migrationBuilder.RenameIndex(name: "IX_WorkoutDays_RevisionId_Order", table: "WorkoutDays", newName: "IX_WorkoutDays_WorkoutPlanId_Order");
            migrationBuilder.RenameColumn(name: "RevisionId", table: "WorkoutPlanNotes", newName: "WorkoutPlanId");
            migrationBuilder.RenameIndex(name: "IX_WorkoutPlanNotes_RevisionId_Order", table: "WorkoutPlanNotes", newName: "IX_WorkoutPlanNotes_WorkoutPlanId_Order");

            migrationBuilder.DropPrimaryKey(name: "PK_DietPlanVariants", table: "DietPlanVariants");
            migrationBuilder.RenameColumn(name: "RevisionId", table: "DietPlanVariants", newName: "DietPlanId");
            migrationBuilder.RenameTable(name: "DietPlanVariants", newName: "DietPlanVersions");
            migrationBuilder.AddPrimaryKey(name: "PK_DietPlanVersions", table: "DietPlanVersions", column: "Id");
            migrationBuilder.DropTable(name: "DietPlanRevisions");
            migrationBuilder.DropTable(name: "WorkoutPlanRevisions");

            migrationBuilder.CreateIndex(name: "IX_DietPlanVersions_DietPlanId_Order", table: "DietPlanVersions",
                columns: new[] { "DietPlanId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_DietPlanMeals_DietPlanVersionId_Order", table: "DietPlanMeals",
                columns: new[] { "DietPlanVersionId", "Order" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_DietReplacementGroups_DietPlanVersionId_Order", table: "DietReplacementGroups",
                columns: new[] { "DietPlanVersionId", "Order" }, unique: true);
            AddCascade(migrationBuilder, "DietPlanVersions", "DietPlanId", "DietPlans");
            AddCascade(migrationBuilder, "DietPlanMeals", "DietPlanVersionId", "DietPlanVersions");
            AddCascade(migrationBuilder, "DietReplacementGroups", "DietPlanVersionId", "DietPlanVersions");
            AddCascade(migrationBuilder, "DietPlanNotes", "DietPlanId", "DietPlans");
            AddCascade(migrationBuilder, "WorkoutDays", "WorkoutPlanId", "WorkoutPlans");
            AddCascade(migrationBuilder, "WorkoutPlanNotes", "WorkoutPlanId", "WorkoutPlans");
        }

        private static void CreateRevisionTable(MigrationBuilder migrationBuilder, string name, string planTable) =>
            migrationBuilder.CreateTable(
                name: name,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey($"PK_{name}", x => x.Id);
                    table.CheckConstraint($"CK_{name}_Dates", "[EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: $"FK_{name}_{planTable}_PlanId",
                        column: x => x.PlanId,
                        principalTable: planTable,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

        private static void CreateRevisionIndexes(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.CreateIndex(name: $"IX_{table}_PlanId_Current", table: table, column: "PlanId",
                unique: true, filter: "[Status] <> 'Archived'");
            migrationBuilder.CreateIndex(name: $"IX_{table}_PlanId_VersionNumber", table: table,
                columns: new[] { "PlanId", "VersionNumber" }, unique: true);
        }

        private static void AddRevisionColumn(MigrationBuilder migrationBuilder, string table) =>
            migrationBuilder.AddColumn<Guid>(name: "RevisionId", table: table, type: "uniqueidentifier", nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        private static void AddCascade(MigrationBuilder migrationBuilder, string table, string column, string principal) =>
            migrationBuilder.AddForeignKey(
                name: $"FK_{table}_{principal}_{column}",
                table: table,
                column: column,
                principalTable: principal,
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
    }
}
