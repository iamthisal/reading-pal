using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendingService.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnsAndFines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservationEvents_ReservationId",
                table: "ReservationEvents");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "ReservationEvents",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE `ReservationEvents` SET `EventType` = JSON_UNQUOTE(JSON_EXTRACT(`Payload`, '$.eventType'));");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnRequestedAtUtc",
                table: "BorrowRecords",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BorrowRecordId = table.Column<int>(type: "int", nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fines_BorrowRecords_BorrowRecordId",
                        column: x => x.BorrowRecordId,
                        principalTable: "BorrowRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_ReservationId_EventType",
                table: "ReservationEvents",
                columns: new[] { "ReservationId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fines_BorrowRecordId",
                table: "Fines",
                column: "BorrowRecordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fines");

            migrationBuilder.DropIndex(
                name: "IX_ReservationEvents_ReservationId_EventType",
                table: "ReservationEvents");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "ReservationEvents");

            migrationBuilder.DropColumn(
                name: "ReturnRequestedAtUtc",
                table: "BorrowRecords");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationEvents_ReservationId",
                table: "ReservationEvents",
                column: "ReservationId",
                unique: true);
        }
    }
}
