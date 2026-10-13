using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendingService.Migrations
{
    /// <inheritdoc />
    public partial class AddDueDateReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueReminderSentAtUtc",
                table: "BorrowRecords",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueReminderSentAtUtc",
                table: "BorrowRecords");
        }
    }
}
