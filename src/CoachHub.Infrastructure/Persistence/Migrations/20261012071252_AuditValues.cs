using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                table: "AuditEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "AuditEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoachId",
                table: "AuditEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ClientId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "ClientId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CoachId_OccurredAt",
                table: "AuditEntries",
                columns: new[] { "CoachId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEntries_Coaches_CoachId",
                table: "AuditEntries",
                column: "CoachId",
                principalTable: "Coaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditEntries_Coaches_CoachId",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_ClientId_OccurredAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CoachId_OccurredAt",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ChangesJson",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "CoachId",
                table: "AuditEntries");
        }
    }
}
