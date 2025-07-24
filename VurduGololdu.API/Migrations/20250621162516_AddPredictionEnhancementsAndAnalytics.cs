using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionEnhancementsAndAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Predictions",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Predictions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Predictions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Predictions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSharedAt",
                table: "Predictions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Predictions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinnedByUserId",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultDate",
                table: "Predictions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultNote",
                table: "Predictions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShareCount",
                table: "Predictions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Predictions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CaptchaVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CaptchaCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CaptchaImageBase64 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptchaVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyAnalytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewUserCount = table.Column<int>(type: "int", nullable: false),
                    ActiveUserCount = table.Column<int>(type: "int", nullable: false),
                    TotalUserCount = table.Column<int>(type: "int", nullable: false),
                    NewPredictionCount = table.Column<int>(type: "int", nullable: false),
                    CompletedPredictionCount = table.Column<int>(type: "int", nullable: false),
                    CorrectPredictionCount = table.Column<int>(type: "int", nullable: false),
                    TotalPredictionCount = table.Column<int>(type: "int", nullable: false),
                    OverallSuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VipSuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    NormalUserSuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DailyRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewVipUserCount = table.Column<int>(type: "int", nullable: false),
                    ExpiredVipUserCount = table.Column<int>(type: "int", nullable: false),
                    TotalLikeCount = table.Column<int>(type: "int", nullable: false),
                    TotalCommentCount = table.Column<int>(type: "int", nullable: false),
                    TotalShareCount = table.Column<int>(type: "int", nullable: false),
                    TotalViewCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSuccessStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TotalPredictions = table.Column<int>(type: "int", nullable: false),
                    CorrectPredictions = table.Column<int>(type: "int", nullable: false),
                    IncorrectPredictions = table.Column<int>(type: "int", nullable: false),
                    PendingPredictions = table.Column<int>(type: "int", nullable: false),
                    SuccessRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CurrentStreak = table.Column<int>(type: "int", nullable: false),
                    BestStreak = table.Column<int>(type: "int", nullable: false),
                    TotalLikes = table.Column<int>(type: "int", nullable: false),
                    TotalComments = table.Column<int>(type: "int", nullable: false),
                    TotalShares = table.Column<int>(type: "int", nullable: false),
                    TotalViews = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSuccessStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSuccessStats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_IsFeatured",
                table: "Predictions",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_IsPinned",
                table: "Predictions",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_IsShared",
                table: "Predictions",
                column: "IsShared");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_PinnedByUserId",
                table: "Predictions",
                column: "PinnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ResultDate",
                table: "Predictions",
                column: "ResultDate");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Status",
                table: "Predictions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CaptchaVerifications_CreatedAt",
                table: "CaptchaVerifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CaptchaVerifications_ExpiresAt",
                table: "CaptchaVerifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CaptchaVerifications_IpAddress",
                table: "CaptchaVerifications",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_CaptchaVerifications_SessionId",
                table: "CaptchaVerifications",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAnalytics_CreatedAt",
                table: "DailyAnalytics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAnalytics_Date",
                table: "DailyAnalytics",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSuccessStats_BestStreak",
                table: "UserSuccessStats",
                column: "BestStreak");

            migrationBuilder.CreateIndex(
                name: "IX_UserSuccessStats_CurrentStreak",
                table: "UserSuccessStats",
                column: "CurrentStreak");

            migrationBuilder.CreateIndex(
                name: "IX_UserSuccessStats_SuccessRate",
                table: "UserSuccessStats",
                column: "SuccessRate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSuccessStats_TotalPredictions",
                table: "UserSuccessStats",
                column: "TotalPredictions");

            migrationBuilder.CreateIndex(
                name: "IX_UserSuccessStats_UserId",
                table: "UserSuccessStats",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_Users_PinnedByUserId",
                table: "Predictions",
                column: "PinnedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_Users_PinnedByUserId",
                table: "Predictions");

            migrationBuilder.DropTable(
                name: "CaptchaVerifications");

            migrationBuilder.DropTable(
                name: "DailyAnalytics");

            migrationBuilder.DropTable(
                name: "UserSuccessStats");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_IsFeatured",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_IsPinned",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_IsShared",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_PinnedByUserId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_ResultDate",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_Status",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "LastSharedAt",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "PinnedByUserId",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ResultDate",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ResultNote",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ShareCount",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Predictions");
        }
    }
}
