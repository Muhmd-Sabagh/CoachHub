using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P9: exercise library v2 (§17: muscles, equipment, difficulty, coaching texts, custom exercises) and typed
    /// workout prescriptions (§16, D3): blocks, set groups and one row per prescribed set.
    /// </summary>
    public partial class WorkoutBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Block",
                table: "WorkoutExercises",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Main");

            migrationBuilder.AddColumn<Guid>(
                name: "SetGroupId",
                table: "WorkoutExercises",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoachId",
                table: "Exercises",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoachingCuesAr",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoachingCuesEn",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommonMistakesAr",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommonMistakesEn",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Exercises",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructionsAr",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructionsEn",
                table: "Exercises",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryMuscleGroupId",
                table: "Exercises",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExerciseEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExerciseEquipment_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MuscleGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuscleGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutPrescribedSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RepsMin = table.Column<int>(type: "int", nullable: true),
                    RepsMax = table.Column<int>(type: "int", nullable: true),
                    LoadKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    Rir = table.Column<int>(type: "int", nullable: true),
                    Rpe = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    TempoEccentric = table.Column<int>(type: "int", nullable: true),
                    TempoBottomPause = table.Column<int>(type: "int", nullable: true),
                    TempoConcentric = table.Column<int>(type: "int", nullable: true),
                    TempoTopPause = table.Column<int>(type: "int", nullable: true),
                    RestSeconds = table.Column<int>(type: "int", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    IsSkippable = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutPrescribedSets", x => x.Id);
                    table.CheckConstraint("CK_WorkoutPrescribedSets_Reps", "[RepsMax] IS NULL OR ([RepsMin] IS NOT NULL AND [RepsMin] <= [RepsMax])");
                    table.CheckConstraint("CK_WorkoutPrescribedSets_Tempo", "([TempoEccentric] IS NULL AND [TempoBottomPause] IS NULL AND [TempoConcentric] IS NULL AND [TempoTopPause] IS NULL) OR ([TempoEccentric] IS NOT NULL AND [TempoBottomPause] IS NOT NULL AND [TempoConcentric] IS NOT NULL AND [TempoTopPause] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkoutPrescribedSets_WorkoutExercises_WorkoutExerciseId",
                        column: x => x.WorkoutExerciseId,
                        principalTable: "WorkoutExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutSetGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RestSeconds = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSetGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutSetGroups_WorkoutDays_WorkoutDayId",
                        column: x => x.WorkoutDayId,
                        principalTable: "WorkoutDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseSecondaryMuscles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MuscleGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseSecondaryMuscles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseSecondaryMuscles_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExerciseSecondaryMuscles_MuscleGroups_MuscleGroupId",
                        column: x => x.MuscleGroupId,
                        principalTable: "MuscleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MuscleGroups",
                columns: new[] { "Id", "IsActive", "NameAr", "NameEn" },
                values: new object[,]
                {
                    { new Guid("0e4d6c1a-0001-4c8e-9a1b-5f3e2d1c0b01"), true, "الصدر", "Chest" },
                    { new Guid("0e4d6c1a-0002-4c8e-9a1b-5f3e2d1c0b02"), true, "الظهر", "Back" },
                    { new Guid("0e4d6c1a-0003-4c8e-9a1b-5f3e2d1c0b03"), true, "الأكتاف", "Shoulders" },
                    { new Guid("0e4d6c1a-0004-4c8e-9a1b-5f3e2d1c0b04"), true, "العضلة ذات الرأسين", "Biceps" },
                    { new Guid("0e4d6c1a-0005-4c8e-9a1b-5f3e2d1c0b05"), true, "العضلة ثلاثية الرؤوس", "Triceps" },
                    { new Guid("0e4d6c1a-0006-4c8e-9a1b-5f3e2d1c0b06"), true, "الساعد", "Forearms" },
                    { new Guid("0e4d6c1a-0007-4c8e-9a1b-5f3e2d1c0b07"), true, "الفخذ الأمامي", "Quadriceps" },
                    { new Guid("0e4d6c1a-0008-4c8e-9a1b-5f3e2d1c0b08"), true, "الفخذ الخلفي", "Hamstrings" },
                    { new Guid("0e4d6c1a-0009-4c8e-9a1b-5f3e2d1c0b09"), true, "الأرداف", "Glutes" },
                    { new Guid("0e4d6c1a-000a-4c8e-9a1b-5f3e2d1c0b0a"), true, "السمانة", "Calves" },
                    { new Guid("0e4d6c1a-000b-4c8e-9a1b-5f3e2d1c0b0b"), true, "البطن", "Abs" },
                    { new Guid("0e4d6c1a-000c-4c8e-9a1b-5f3e2d1c0b0c"), true, "أسفل الظهر", "Lower back" },
                    { new Guid("0e4d6c1a-000d-4c8e-9a1b-5f3e2d1c0b0d"), true, "الترابيس", "Traps" },
                    { new Guid("0e4d6c1a-000e-4c8e-9a1b-5f3e2d1c0b0e"), true, "العضلات المقربة", "Adductors" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutExercises_SetGroupId",
                table: "WorkoutExercises",
                column: "SetGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_CoachId",
                table: "Exercises",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_PrimaryMuscleGroupId",
                table: "Exercises",
                column: "PrimaryMuscleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseEquipment_EquipmentId",
                table: "ExerciseEquipment",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseEquipment_ExerciseId_EquipmentId",
                table: "ExerciseEquipment",
                columns: new[] { "ExerciseId", "EquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSecondaryMuscles_ExerciseId_MuscleGroupId",
                table: "ExerciseSecondaryMuscles",
                columns: new[] { "ExerciseId", "MuscleGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseSecondaryMuscles_MuscleGroupId",
                table: "ExerciseSecondaryMuscles",
                column: "MuscleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MuscleGroups_NameEn",
                table: "MuscleGroups",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPrescribedSets_RevisionId",
                table: "WorkoutPrescribedSets",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPrescribedSets_WorkoutExerciseId_Order",
                table: "WorkoutPrescribedSets",
                columns: new[] { "WorkoutExerciseId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSetGroups_RevisionId",
                table: "WorkoutSetGroups",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSetGroups_WorkoutDayId",
                table: "WorkoutSetGroups",
                column: "WorkoutDayId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exercises_Coaches_CoachId",
                table: "Exercises",
                column: "CoachId",
                principalTable: "Coaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exercises_MuscleGroups_PrimaryMuscleGroupId",
                table: "Exercises",
                column: "PrimaryMuscleGroupId",
                principalTable: "MuscleGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutExercises_WorkoutSetGroups_SetGroupId",
                table: "WorkoutExercises",
                column: "SetGroupId",
                principalTable: "WorkoutSetGroups",
                principalColumn: "Id");
            // Free-text prescriptions become typed sets (D3) where they parse: "3" sets of "8-12" reps, rest "90"
            // or "90s", tempo "3010" or "3-0-1-0". The original text is kept in the exercise notes.
            migrationBuilder.Sql("""
                WITH [Numbers] AS (SELECT [n] FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16),(17),(18),(19)) AS [v]([n]))
                INSERT INTO [WorkoutPrescribedSets] ([Id], [RevisionId], [WorkoutExerciseId], [Order], [Type], [RepsMin], [RepsMax],
                    [LoadKg], [Rir], [Rpe], [TempoEccentric], [TempoBottomPause], [TempoConcentric], [TempoTopPause],
                    [RestSeconds], [DurationSeconds], [IsSkippable], [Notes])
                SELECT NEWID(), [e].[RevisionId], [e].[Id], [n].[n], N'Normal',
                    CASE WHEN [r].[Low] IS NOT NULL AND ([r].[High] IS NULL OR [r].[High] >= [r].[Low]) THEN [r].[Low] END,
                    CASE WHEN [r].[Low] IS NOT NULL AND [r].[High] >= [r].[Low] THEN [r].[High] END,
                    NULL, NULL, NULL,
                    CASE WHEN [t].[Digits] IS NOT NULL THEN CAST(SUBSTRING([t].[Digits], 1, 1) AS int) END,
                    CASE WHEN [t].[Digits] IS NOT NULL THEN CAST(SUBSTRING([t].[Digits], 2, 1) AS int) END,
                    CASE WHEN [t].[Digits] IS NOT NULL THEN CAST(SUBSTRING([t].[Digits], 3, 1) AS int) END,
                    CASE WHEN [t].[Digits] IS NOT NULL THEN CAST(SUBSTRING([t].[Digits], 4, 1) AS int) END,
                    CASE WHEN [s].[Rest] BETWEEN 0 AND 3600 THEN [s].[Rest] END,
                    NULL, 0, NULL
                FROM [WorkoutExercises] AS [e]
                CROSS APPLY (SELECT
                    CASE WHEN TRY_CAST(LTRIM(RTRIM([e].[Sets])) AS int) BETWEEN 1 AND 20 THEN TRY_CAST(LTRIM(RTRIM([e].[Sets])) AS int) ELSE 1 END AS [Count],
                    TRY_CAST(REPLACE(REPLACE(LOWER(LTRIM(RTRIM([e].[Rest]))), N'sec', N''), N's', N'') AS int) AS [Rest],
                    REPLACE(REPLACE(ISNULL([e].[Repetitions], N''), N' ', N''), NCHAR(8211), N'-') AS [Reps],
                    REPLACE(REPLACE(ISNULL([e].[Tempo], N''), N'-', N''), N' ', N'') AS [Tempo]) AS [s]
                CROSS APPLY (SELECT
                    CASE WHEN CHARINDEX(N'-', [s].[Reps]) > 1 THEN TRY_CAST(LEFT([s].[Reps], CHARINDEX(N'-', [s].[Reps]) - 1) AS int)
                         ELSE TRY_CAST(NULLIF([s].[Reps], N'') AS int) END AS [Low],
                    CASE WHEN CHARINDEX(N'-', [s].[Reps]) > 1 THEN TRY_CAST(SUBSTRING([s].[Reps], CHARINDEX(N'-', [s].[Reps]) + 1, 10) AS int) END AS [High]) AS [r]
                CROSS APPLY (SELECT CASE WHEN LEN([s].[Tempo]) = 4 AND [s].[Tempo] NOT LIKE N'%[^0-9]%' THEN [s].[Tempo] END AS [Digits]) AS [t]
                JOIN [Numbers] AS [n] ON [n].[n] < [s].[Count];

                UPDATE [WorkoutExercises]
                SET [Notes] = LEFT(CONCAT([Notes] + NCHAR(10), N'Previous prescription: ',
                    CONCAT_WS(N', ', N'sets ' + [Sets], N'reps ' + [Repetitions], N'rest ' + [Rest], N'tempo ' + [Tempo], N'RPE/RIR ' + [RpeRir])), 1000)
                WHERE COALESCE([Sets], [Repetitions], [Rest], [Tempo], [RpeRir]) IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "Rest",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "RpeRir",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "Tempo",
                table: "WorkoutExercises");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Repetitions",
                table: "WorkoutExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rest",
                table: "WorkoutExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RpeRir",
                table: "WorkoutExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sets",
                table: "WorkoutExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tempo",
                table: "WorkoutExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // The previous columns come back with the first set's prescription as text.
            migrationBuilder.Sql("""
                UPDATE [e]
                SET [Sets] = CAST([c].[Count] AS nvarchar(50)),
                    [Repetitions] = CASE WHEN [f].[RepsMax] IS NOT NULL THEN CONCAT([f].[RepsMin], N'-', [f].[RepsMax]) ELSE CAST([f].[RepsMin] AS nvarchar(50)) END,
                    [Rest] = CAST([f].[RestSeconds] AS nvarchar(50)),
                    [Tempo] = CASE WHEN [f].[TempoEccentric] IS NOT NULL THEN CONCAT([f].[TempoEccentric], [f].[TempoBottomPause], [f].[TempoConcentric], [f].[TempoTopPause]) END,
                    [RpeRir] = CONCAT_WS(N' ', N'RPE ' + CAST([f].[Rpe] AS nvarchar(10)), N'RIR ' + CAST([f].[Rir] AS nvarchar(10)))
                FROM [WorkoutExercises] AS [e]
                CROSS APPLY (SELECT COUNT(*) AS [Count] FROM [WorkoutPrescribedSets] AS [x] WHERE [x].[WorkoutExerciseId] = [e].[Id]) AS [c]
                OUTER APPLY (SELECT TOP (1) * FROM [WorkoutPrescribedSets] AS [x] WHERE [x].[WorkoutExerciseId] = [e].[Id] ORDER BY [x].[Order]) AS [f];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Exercises_Coaches_CoachId",
                table: "Exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_Exercises_MuscleGroups_PrimaryMuscleGroupId",
                table: "Exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutExercises_WorkoutSetGroups_SetGroupId",
                table: "WorkoutExercises");

            migrationBuilder.DropTable(
                name: "ExerciseEquipment");

            migrationBuilder.DropTable(
                name: "ExerciseSecondaryMuscles");

            migrationBuilder.DropTable(
                name: "WorkoutPrescribedSets");

            migrationBuilder.DropTable(
                name: "WorkoutSetGroups");

            migrationBuilder.DropTable(
                name: "MuscleGroups");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutExercises_SetGroupId",
                table: "WorkoutExercises");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_CoachId",
                table: "Exercises");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_PrimaryMuscleGroupId",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "Block",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "SetGroupId",
                table: "WorkoutExercises");

            migrationBuilder.DropColumn(
                name: "CoachId",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CoachingCuesAr",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CoachingCuesEn",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CommonMistakesAr",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "CommonMistakesEn",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "InstructionsAr",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "InstructionsEn",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "PrimaryMuscleGroupId",
                table: "Exercises");
        }
    }
}
