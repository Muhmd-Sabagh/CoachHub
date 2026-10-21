using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OneRepMaxFormula",
                table: "CoachSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                // Saved coach settings keep the default formula (§22).
                defaultValue: "Epley");

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryMuscleWeight",
                table: "CoachSettings",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PersonalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PreviousValue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Reps = table.Column<int>(type: "int", nullable: true),
                    WorkoutSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoggedSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AchievedOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRecords", x => x.Id);
                    table.CheckConstraint("CK_PersonalRecords_Value", "[Value] > 0 AND ([PreviousValue] IS NULL OR [PreviousValue] < [Value])");
                    table.ForeignKey(
                        name: "FK_PersonalRecords_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: true),
                    WorkoutDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DayOrder = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSessions", x => x.Id);
                    table.CheckConstraint("CK_WorkoutSessions_Completed", "([Status] = 'Completed' AND [CompletedAt] IS NOT NULL) OR ([Status] = 'InProgress' AND [CompletedAt] IS NULL)");
                    table.CheckConstraint("CK_WorkoutSessions_PlannedDay", "([WorkoutDayId] IS NULL AND [PlanId] IS NULL AND [RevisionId] IS NULL AND [VersionNumber] IS NULL AND [DayOrder] IS NULL) OR ([WorkoutDayId] IS NOT NULL AND [PlanId] IS NOT NULL AND [RevisionId] IS NOT NULL AND [VersionNumber] IS NOT NULL AND [DayOrder] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutLoggedExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Block = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsSkipped = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutLoggedExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutLoggedExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutLoggedExercises_WorkoutSessions_WorkoutSessionId",
                        column: x => x.WorkoutSessionId,
                        principalTable: "WorkoutSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutLoggedSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoggedExerciseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reps = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Rir = table.Column<int>(type: "int", nullable: true),
                    Rpe = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoggedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutLoggedSets", x => x.Id);
                    table.CheckConstraint("CK_WorkoutLoggedSets_Ranges", "([Reps] IS NULL OR [Reps] BETWEEN 0 AND 1000) AND ([WeightKg] IS NULL OR [WeightKg] BETWEEN 0 AND 1000) AND ([Rir] IS NULL OR [Rir] BETWEEN 0 AND 10) AND ([Rpe] IS NULL OR [Rpe] BETWEEN 1 AND 10) AND ([DurationSeconds] IS NULL OR [DurationSeconds] BETWEEN 1 AND 36000)");
                    table.CheckConstraint("CK_WorkoutLoggedSets_Values", "([Status] = 'Skipped' AND [Reps] IS NULL AND [WeightKg] IS NULL AND [Rir] IS NULL AND [Rpe] IS NULL AND [DurationSeconds] IS NULL) OR ([Status] = 'Completed' AND ([Reps] IS NOT NULL OR [DurationSeconds] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_WorkoutLoggedSets_WorkoutLoggedExercises_LoggedExerciseId",
                        column: x => x.LoggedExerciseId,
                        principalTable: "WorkoutLoggedExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_ClientId_ExerciseId_Type",
                table: "PersonalRecords",
                columns: new[] { "ClientId", "ExerciseId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_CoachId",
                table: "PersonalRecords",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_ExerciseId",
                table: "PersonalRecords",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLoggedExercises_ExerciseId",
                table: "WorkoutLoggedExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLoggedExercises_WorkoutSessionId",
                table: "WorkoutLoggedExercises",
                column: "WorkoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLoggedSets_LoggedExerciseId",
                table: "WorkoutLoggedSets",
                column: "LoggedExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_ClientId_Date",
                table: "WorkoutSessions",
                columns: new[] { "ClientId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_ClientId_InProgress",
                table: "WorkoutSessions",
                column: "ClientId",
                unique: true,
                filter: "[Status] = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_CoachId",
                table: "WorkoutSessions",
                column: "CoachId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalRecords");

            migrationBuilder.DropTable(
                name: "WorkoutLoggedSets");

            migrationBuilder.DropTable(
                name: "WorkoutLoggedExercises");

            migrationBuilder.DropTable(
                name: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "OneRepMaxFormula",
                table: "CoachSettings");

            migrationBuilder.DropColumn(
                name: "SecondaryMuscleWeight",
                table: "CoachSettings");
        }
    }
}
