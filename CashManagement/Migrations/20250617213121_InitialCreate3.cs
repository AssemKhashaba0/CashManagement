using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransactionsPhysical_AspNetUsers_ApplicationUserId",
                table: "CashTransactionsPhysical");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactionsPhysical_ApplicationUserId",
                table: "CashTransactionsPhysical");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "CashTransactionsPhysical");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactionsPhysical_UserId",
                table: "CashTransactionsPhysical",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransactionsPhysical_AspNetUsers_UserId",
                table: "CashTransactionsPhysical",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashTransactionsPhysical_AspNetUsers_UserId",
                table: "CashTransactionsPhysical");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactionsPhysical_UserId",
                table: "CashTransactionsPhysical");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "CashTransactionsPhysical",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactionsPhysical_ApplicationUserId",
                table: "CashTransactionsPhysical",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashTransactionsPhysical_AspNetUsers_ApplicationUserId",
                table: "CashTransactionsPhysical",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
