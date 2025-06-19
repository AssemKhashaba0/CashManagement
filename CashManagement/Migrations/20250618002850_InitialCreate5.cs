using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "InstaPays",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstaPays_UserId",
                table: "InstaPays",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstaPays_AspNetUsers_UserId",
                table: "InstaPays",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstaPays_AspNetUsers_UserId",
                table: "InstaPays");

            migrationBuilder.DropIndex(
                name: "IX_InstaPays_UserId",
                table: "InstaPays");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "InstaPays");
        }
    }
}
