using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInListIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_CoachId_SubmittedAt",
                table: "CheckIns",
                columns: new[] { "CoachId", "SubmittedAt" })
                .Annotation("SqlServer:Include", new[] { "ClientId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckIns_CoachId_SubmittedAt",
                table: "CheckIns");
        }
    }
}
