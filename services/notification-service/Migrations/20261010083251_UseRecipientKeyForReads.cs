using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Migrations
{
    /// <inheritdoc />
    public partial class UseRecipientKeyForReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientKey",
                table: "NotificationReads",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Existing reads were all customers reading announcements: keep them as "user:<id>".
            migrationBuilder.Sql("UPDATE `NotificationReads` SET `RecipientKey` = CONCAT('user:', `UserId`);");

            // MySQL needs an index on NotificationId for the foreign key, so create the new index before
            // dropping the old one.
            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_NotificationId_RecipientKey",
                table: "NotificationReads",
                columns: new[] { "NotificationId", "RecipientKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_NotificationReads_NotificationId_UserId",
                table: "NotificationReads");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "NotificationReads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema only tracked customers; admin reads cannot be represented.
            migrationBuilder.Sql("DELETE FROM `NotificationReads` WHERE `RecipientKey` NOT LIKE 'user:%';");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "NotificationReads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE `NotificationReads` SET `UserId` = CAST(SUBSTRING(`RecipientKey`, 6) AS UNSIGNED);");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_NotificationId_UserId",
                table: "NotificationReads",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_NotificationReads_NotificationId_RecipientKey",
                table: "NotificationReads");

            migrationBuilder.DropColumn(
                name: "RecipientKey",
                table: "NotificationReads");
        }
    }
}
