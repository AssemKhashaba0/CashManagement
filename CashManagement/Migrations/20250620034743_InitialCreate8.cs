using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierTransactions_AspNetUsers_ApplicationUserId",
                table: "SupplierTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SupplierTransactions_ApplicationUserId",
                table: "SupplierTransactions");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "SupplierTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierTransactions_UserId",
                table: "SupplierTransactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierTransactions_AspNetUsers_UserId",
                table: "SupplierTransactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierTransactions_AspNetUsers_UserId",
                table: "SupplierTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SupplierTransactions_UserId",
                table: "SupplierTransactions");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "SupplierTransactions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierTransactions_ApplicationUserId",
                table: "SupplierTransactions",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierTransactions_AspNetUsers_ApplicationUserId",
                table: "SupplierTransactions",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
