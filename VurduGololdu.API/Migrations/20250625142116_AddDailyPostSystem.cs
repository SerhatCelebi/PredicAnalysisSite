using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPostSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyPostId",
                table: "Likes",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PredictionId",
                table: "Comments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DailyPostId",
                table: "Comments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPosts_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Likes_DailyPostId",
                table: "Likes",
                column: "DailyPostId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId_DailyPostId",
                table: "Likes",
                columns: new[] { "UserId", "DailyPostId" },
                unique: true,
                filter: "[DailyPostId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DailyPostId",
                table: "Comments",
                column: "DailyPostId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPosts_AdminId",
                table: "DailyPosts",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPosts_Category",
                table: "DailyPosts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPosts_CreatedAt",
                table: "DailyPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPosts_IsFeatured",
                table: "DailyPosts",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPosts_IsPublished",
                table: "DailyPosts",
                column: "IsPublished");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_DailyPosts_DailyPostId",
                table: "Comments",
                column: "DailyPostId",
                principalTable: "DailyPosts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_DailyPosts_DailyPostId",
                table: "Likes",
                column: "DailyPostId",
                principalTable: "DailyPosts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_DailyPosts_DailyPostId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_DailyPosts_DailyPostId",
                table: "Likes");

            migrationBuilder.DropTable(
                name: "DailyPosts");

            migrationBuilder.DropIndex(
                name: "IX_Likes_DailyPostId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Likes_UserId_DailyPostId",
                table: "Likes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_DailyPostId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "DailyPostId",
                table: "Likes");

            migrationBuilder.DropColumn(
                name: "DailyPostId",
                table: "Comments");

            migrationBuilder.AlterColumn<int>(
                name: "PredictionId",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
