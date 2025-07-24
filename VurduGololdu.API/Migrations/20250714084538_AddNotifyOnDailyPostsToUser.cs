using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifyOnDailyPostsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnDailyPosts",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyOnDailyPosts",
                table: "Users");
        }
    }
}
