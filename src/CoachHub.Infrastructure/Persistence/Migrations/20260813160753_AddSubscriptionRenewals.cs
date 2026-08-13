using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionRenewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRenewals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewals_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewals_PaymentAccounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "PaymentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewals_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewals_CurrencyId",
                table: "SubscriptionRenewals",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewals_PaymentAccountId",
                table: "SubscriptionRenewals",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewals_RecordedAt",
                table: "SubscriptionRenewals",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewals_SubscriptionId_SequenceNumber",
                table: "SubscriptionRenewals",
                columns: new[] { "SubscriptionId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionRenewals");
        }
    }
}
