using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metric",
                table: "FormQuestions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CheckInAwaitingReview",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckInDueOn",
                table: "Clients",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PrivateNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckIns_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIns_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CheckIns_FormSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CheckInSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntervalWeeks = table.Column<int>(type: "int", nullable: false),
                    DueWeekday = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInSchedules", x => x.Id);
                    table.CheckConstraint("CK_CheckInSchedules_Interval", "[IntervalWeeks] IN (1, 2, 4)");
                    table.ForeignKey(
                        name: "FK_CheckInSchedules_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInSchedules_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckInSchedules_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CheckInReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInReplies_CheckIns_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckInSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInSubmissions_CheckIns_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "CheckIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInSubmissions_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInReplies_CheckInId",
                table: "CheckInReplies",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_ClientId_DueOn",
                table: "CheckIns",
                columns: new[] { "ClientId", "DueOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_CoachId_Status_SubmittedAt",
                table: "CheckIns",
                columns: new[] { "CoachId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_FormDefinitionId",
                table: "CheckIns",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_SubmissionId",
                table: "CheckIns",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedules_ClientId",
                table: "CheckInSchedules",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedules_CoachId",
                table: "CheckInSchedules",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedules_FormDefinitionId",
                table: "CheckInSchedules",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSubmissions_CheckInId",
                table: "CheckInSubmissions",
                column: "CheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSubmissions_FormSubmissionId",
                table: "CheckInSubmissions",
                column: "FormSubmissionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInReplies");

            migrationBuilder.DropTable(
                name: "CheckInSchedules");

            migrationBuilder.DropTable(
                name: "CheckInSubmissions");

            migrationBuilder.DropTable(
                name: "CheckIns");

            migrationBuilder.DropColumn(
                name: "Metric",
                table: "FormQuestions");

            migrationBuilder.DropColumn(
                name: "CheckInAwaitingReview",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CheckInDueOn",
                table: "Clients");
        }
    }
}
