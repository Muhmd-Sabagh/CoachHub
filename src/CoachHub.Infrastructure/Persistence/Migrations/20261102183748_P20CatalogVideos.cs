using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P20CatalogVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VideoMediaId",
                table: "TestDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VideoMediaId",
                table: "Exercises",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0001-4b6e-8c2d-1a9e5f000001"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0002-4b6e-8c2d-1a9e5f000002"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0003-4b6e-8c2d-1a9e5f000003"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0004-4b6e-8c2d-1a9e5f000004"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0005-4b6e-8c2d-1a9e5f000005"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0006-4b6e-8c2d-1a9e5f000006"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0007-4b6e-8c2d-1a9e5f000007"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0008-4b6e-8c2d-1a9e5f000008"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-0009-4b6e-8c2d-1a9e5f000009"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000a-4b6e-8c2d-1a9e5f00000a"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000b-4b6e-8c2d-1a9e5f00000b"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000c-4b6e-8c2d-1a9e5f00000c"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000d-4b6e-8c2d-1a9e5f00000d"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000e-4b6e-8c2d-1a9e5f00000e"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "TestDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("7d1f3a20-000f-4b6e-8c2d-1a9e5f00000f"),
                column: "VideoMediaId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitions_VideoMediaId",
                table: "TestDefinitions",
                column: "VideoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_VideoMediaId",
                table: "Exercises",
                column: "VideoMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exercises_Media_VideoMediaId",
                table: "Exercises",
                column: "VideoMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinitions_Media_VideoMediaId",
                table: "TestDefinitions",
                column: "VideoMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exercises_Media_VideoMediaId",
                table: "Exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinitions_Media_VideoMediaId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinitions_VideoMediaId",
                table: "TestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_VideoMediaId",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "VideoMediaId",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "VideoMediaId",
                table: "Exercises");
        }
    }
}
