using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FormType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSections_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InitialClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormSubmissions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FormSubmissions_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FormQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StableKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormQuestions_FormSections_FormSectionId",
                        column: x => x.FormSectionId,
                        principalTable: "FormSections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FormQuestions_FormVersions_FormVersionId",
                        column: x => x.FormVersionId,
                        principalTable: "FormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionStableKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionTextSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QuestionTypeSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormAnswers_FormQuestions_FormQuestionId",
                        column: x => x.FormQuestionId,
                        principalTable: "FormQuestions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FormAnswers_FormSubmissions_FormSubmissionId",
                        column: x => x.FormSubmissionId,
                        principalTable: "FormSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormAnswers_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_FormQuestions_FormQuestionId",
                        column: x => x.FormQuestionId,
                        principalTable: "FormQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormAnswers_FormQuestionId",
                table: "FormAnswers",
                column: "FormQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormAnswers_FormSubmissionId_FormQuestionId",
                table: "FormAnswers",
                columns: new[] { "FormSubmissionId", "FormQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormAnswers_MediaId",
                table: "FormAnswers",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_FormQuestions_FormSectionId",
                table: "FormQuestions",
                column: "FormSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormQuestions_FormVersionId_Order",
                table: "FormQuestions",
                columns: new[] { "FormVersionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_FormQuestions_FormVersionId_StableKey",
                table: "FormQuestions",
                columns: new[] { "FormVersionId", "StableKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormSections_FormVersionId_Order",
                table: "FormSections",
                columns: new[] { "FormVersionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_ClientId_SubmittedAt",
                table: "FormSubmissions",
                columns: new[] { "ClientId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_FormDefinitionId",
                table: "FormSubmissions",
                column: "FormDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_FormVersionId",
                table: "FormSubmissions",
                column: "FormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormSubmissions_InitialClientId",
                table: "FormSubmissions",
                column: "InitialClientId",
                unique: true,
                filter: "[InitialClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormDefinitionId",
                table: "FormVersions",
                column: "FormDefinitionId",
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormDefinitionId_VersionNumber",
                table: "FormVersions",
                columns: new[] { "FormDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_FormQuestionId_Value",
                table: "QuestionOptions",
                columns: new[] { "FormQuestionId", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormAnswers");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "FormSubmissions");

            migrationBuilder.DropTable(
                name: "FormQuestions");

            migrationBuilder.DropTable(
                name: "FormSections");

            migrationBuilder.DropTable(
                name: "FormVersions");

            migrationBuilder.DropTable(
                name: "FormDefinitions");
        }
    }
}
