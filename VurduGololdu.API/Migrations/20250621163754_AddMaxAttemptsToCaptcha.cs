using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VurduGololdu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxAttemptsToCaptcha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "CaptchaVerifications",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "CaptchaVerifications");
        }
    }
}
