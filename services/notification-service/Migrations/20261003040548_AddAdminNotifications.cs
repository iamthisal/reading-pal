using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EventId",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Notifications",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                // Every notification created before this migration was a customer notification.
                defaultValue: "User")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservationDate",
                table: "Notifications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Audience_IsRead_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "Audience", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId_Audience",
                table: "Notifications",
                columns: new[] { "EventId", "Audience" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_Audience_IsRead_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_EventId_Audience",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReservationDate",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId",
                table: "Notifications",
                column: "EventId",
                unique: true);
        }
    }
}
