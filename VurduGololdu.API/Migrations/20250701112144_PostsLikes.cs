using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class PostsLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "Predictions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Predictions",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
