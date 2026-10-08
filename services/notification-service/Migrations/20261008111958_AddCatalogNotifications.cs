using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EventId_Audience",
                table: "Notifications");

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReservationStates",
                columns: table => new
                {
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationStates", x => x.ReservationId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId_Audience_UserId",
                table: "Notifications",
                columns: new[] { "EventId", "Audience", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_NotificationId_UserId",
                table: "NotificationReads",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationStates_BookId_Status",
                table: "ReservationStates",
                columns: new[] { "BookId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "ReservationStates");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_EventId_Audience_UserId",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EventId_Audience",
                table: "Notifications",
                columns: new[] { "EventId", "Audience" },
                unique: true);
        }
    }
}
