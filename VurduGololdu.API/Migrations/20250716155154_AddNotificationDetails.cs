using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableEmailNotifications",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "NotificationLogs");

            migrationBuilder.RenameColumn(
                name: "ExternalId",
                table: "NotificationLogs",
                newName: "ActorLastName");

            migrationBuilder.AddColumn<string>(
                name: "ActorFirstName",
                table: "NotificationLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorProfileImageUrl",
                table: "NotificationLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorUserId",
                table: "NotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedLink",
                table: "NotificationLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_ActorUserId",
                table: "NotificationLogs",
                column: "ActorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_ActorUserId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ActorFirstName",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ActorProfileImageUrl",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "RelatedLink",
                table: "NotificationLogs");

            migrationBuilder.RenameColumn(
                name: "ActorLastName",
                table: "NotificationLogs",
                newName: "ExternalId");

            migrationBuilder.AddColumn<bool>(
                name: "EnableEmailNotifications",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "NotificationLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "NotificationLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "NotificationLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "NotificationLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "NotificationLogs",
                type: "datetime2",
                nullable: true);
        }
    }
}
