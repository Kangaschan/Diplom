using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    public partial class AddRecurringPaymentsAutomation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "recurring_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "recurring_payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "recurring_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextExecutionAt",
                table: "recurring_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "recurring_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "recurring_payments",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringPaymentId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_RecurringPaymentId",
                table: "transactions",
                column: "RecurringPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_payments_AccountId",
                table: "recurring_payments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_payments_UserId_IsActive_NextExecutionAt",
                table: "recurring_payments",
                columns: new[] { "UserId", "IsActive", "NextExecutionAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_RecurringPaymentId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_recurring_payments_AccountId",
                table: "recurring_payments");

            migrationBuilder.DropIndex(
                name: "IX_recurring_payments_UserId_IsActive_NextExecutionAt",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "NextExecutionAt",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "recurring_payments");

            migrationBuilder.DropColumn(
                name: "RecurringPaymentId",
                table: "transactions");
        }
    }
}
