using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInSchedulePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "InForceFrom",
                table: "CheckInSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckInSchedulePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntervalWeeks = table.Column<int>(type: "int", nullable: false),
                    DueWeekday = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    InForceFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    InForceTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInSchedulePeriods", x => x.Id);
                    table.CheckConstraint("CK_CheckInSchedulePeriods_Interval", "[IntervalWeeks] IN (1, 2, 4)");
                    table.CheckConstraint("CK_CheckInSchedulePeriods_Range", "[InForceTo] >= [InForceFrom]");
                    table.ForeignKey(
                        name: "FK_CheckInSchedulePeriods_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckInSchedulePeriods_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckInSchedulePeriods_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedulePeriods_ClientId_InForceFrom",
                table: "CheckInSchedulePeriods",
                columns: new[] { "ClientId", "InForceFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedulePeriods_CoachId",
                table: "CheckInSchedulePeriods",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSchedulePeriods_FormDefinitionId",
                table: "CheckInSchedulePeriods",
                column: "FormDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInSchedulePeriods");

            migrationBuilder.DropColumn(
                name: "InForceFrom",
                table: "CheckInSchedules");
        }
    }
}
