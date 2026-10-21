using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FitnessTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HigherIsBetter = table.Column<bool>(type: "bit", nullable: false),
                    GoodThreshold = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    PoorThreshold = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    InstructionsEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InstructionsAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinitions", x => x.Id);
                    table.CheckConstraint("CK_TestDefinitions_CutOffs", "([GoodThreshold] IS NULL AND [PoorThreshold] IS NULL) OR ([GoodThreshold] IS NOT NULL AND [PoorThreshold] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TestDefinitions_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAssignments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestAssignments_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestAssignments_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HigherIsBetter = table.Column<bool>(type: "bit", nullable: false),
                    GoodThreshold = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    PoorThreshold = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsClassificationOverridden = table.Column<bool>(type: "bit", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResults", x => x.Id);
                    table.CheckConstraint("CK_TestResults_Value", "[Value] >= 0");
                    table.ForeignKey(
                        name: "FK_TestResults_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestResults_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestResults_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "TestDefinitions",
                columns: new[] { "Id", "CoachId", "GoodThreshold", "HigherIsBetter", "InstructionsAr", "InstructionsEn", "IsActive", "Kind", "NameAr", "NameEn", "PoorThreshold", "Unit", "YouTubeUrl" },
                values: new object[,]
                {
                    { new Guid("7d1f3a20-0001-4b6e-8c2d-1a9e5f000001"), null, null, true, null, "Sit with legs straight and feet against the box. Reach forward slowly and hold for two seconds. Record the best of three reaches.", true, "Mobility", "الجلوس والوصول", "Sit and reach", null, "Centimeters", null },
                    { new Guid("7d1f3a20-0002-4b6e-8c2d-1a9e5f000002"), null, null, true, null, "Hold a dowel overhead with straight arms and squat as deep as you can. Score 0 (pain) to 3 (full depth, heels down, torso upright).", true, "Mobility", "القرفصاء مع رفع الذراعين", "Overhead squat", null, "Points", null },
                    { new Guid("7d1f3a20-0003-4b6e-8c2d-1a9e5f000003"), null, null, false, null, "Reach one hand over the shoulder and the other up the back. Measure the gap between the fists; record 0 when they touch.", true, "Mobility", "مرونة الكتف", "Shoulder reach", null, "Centimeters", null },
                    { new Guid("7d1f3a20-0004-4b6e-8c2d-1a9e5f000004"), null, null, true, null, "Lunge facing a wall and push the knee to the wall with the heel down. Record the longest toe-to-wall distance.", true, "Mobility", "الركبة إلى الحائط", "Knee to wall", null, "Centimeters", null },
                    { new Guid("7d1f3a20-0005-4b6e-8c2d-1a9e5f000005"), null, null, true, null, "Warm up, then work up to the heaviest single repetition with full range and a spotter.", true, "Strength", "ضغط البنش - أقصى تكرار واحد", "Bench press 1RM", null, "Kilograms", null },
                    { new Guid("7d1f3a20-0006-4b6e-8c2d-1a9e5f000006"), null, null, true, null, "Warm up, then work up to the heaviest single repetition to at least parallel depth, with safety bars set.", true, "Strength", "القرفصاء الخلفي - أقصى تكرار واحد", "Back squat 1RM", null, "Kilograms", null },
                    { new Guid("7d1f3a20-0007-4b6e-8c2d-1a9e5f000007"), null, null, true, null, "Warm up, then work up to the heaviest single repetition with a neutral spine.", true, "Strength", "الرفعة المميتة - أقصى تكرار واحد", "Deadlift 1RM", null, "Kilograms", null },
                    { new Guid("7d1f3a20-0008-4b6e-8c2d-1a9e5f000008"), null, null, true, null, "Do as many full push-ups as you can without resting, chest to a fist's height from the floor.", true, "Strength", "تمرين الضغط - أقصى عدد", "Push-ups (max reps)", null, "Repetitions", null },
                    { new Guid("7d1f3a20-0009-4b6e-8c2d-1a9e5f000009"), null, null, true, null, "From a dead hang, pull the chin over the bar as many times as you can without kipping.", true, "Strength", "العقلة - أقصى عدد", "Pull-ups (max reps)", null, "Repetitions", null },
                    { new Guid("7d1f3a20-000a-4b6e-8c2d-1a9e5f00000a"), null, null, false, null, "Run 2.4 km (1.5 miles) on a flat course as fast as you can. Record the time in seconds.", true, "Endurance", "جري 2.4 كم", "2.4 km run", null, "Seconds", null },
                    { new Guid("7d1f3a20-000b-4b6e-8c2d-1a9e5f00000b"), null, null, true, null, "Run or walk as far as you can in 12 minutes on a flat course. Record the distance in metres.", true, "Endurance", "جري 12 دقيقة", "12-minute run", null, "Meters", null },
                    { new Guid("7d1f3a20-000c-4b6e-8c2d-1a9e5f00000c"), null, null, false, null, "Walk 1.6 km (1 mile) as fast as you can without running. Record the time in seconds.", true, "Endurance", "مشي 1.6 كم", "1.6 km walk", null, "Seconds", null },
                    { new Guid("7d1f3a20-000d-4b6e-8c2d-1a9e5f00000d"), null, null, true, null, "Do as many full push-ups as you can in 60 seconds; resting in the top position is allowed.", true, "Endurance", "تمرين الضغط في دقيقة", "Push-ups in 1 minute", null, "Repetitions", null },
                    { new Guid("7d1f3a20-000e-4b6e-8c2d-1a9e5f00000e"), null, null, true, null, "Hold a forearm plank with a straight line from head to heels. Stop when the hips drop; record the time in seconds.", true, "Endurance", "ثبات البلانك", "Plank hold", null, "Seconds", null },
                    { new Guid("7d1f3a20-000f-4b6e-8c2d-1a9e5f00000f"), null, null, false, null, "Step up and down a 30 cm step at 96 beats per minute for 3 minutes, sit, and count the heart rate for one minute.", true, "Endurance", "اختبار الصعود 3 دقائق", "3-minute step test", null, "BeatsPerMinute", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_ClientId_TestDefinitionId",
                table: "TestAssignments",
                columns: new[] { "ClientId", "TestDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_CoachId",
                table: "TestAssignments",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_TestDefinitionId",
                table: "TestAssignments",
                column: "TestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_CoachId_Kind_NameEn",
                table: "TestDefinitions",
                columns: new[] { "CoachId", "Kind", "NameEn" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_ClientId_TestDefinitionId_Date",
                table: "TestResults",
                columns: new[] { "ClientId", "TestDefinitionId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_CoachId",
                table: "TestResults",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_TestDefinitionId",
                table: "TestResults",
                column: "TestDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestAssignments");

            migrationBuilder.DropTable(
                name: "TestResults");

            migrationBuilder.DropTable(
                name: "TestDefinitions");
        }
    }
}
