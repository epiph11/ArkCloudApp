using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStockAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_products_Category",
                table: "products",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_Category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "products");
        }
    }
}
