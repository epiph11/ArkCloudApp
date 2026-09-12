using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkCloud.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// RGPD finding (docs/rgpd-classification-donnees.md §3): orders.CustomerId never had a
    /// foreign key at the database level, so a hard delete of a Customer with existing orders
    /// silently left it orphaned. CustomerAppService.DeleteAsync now anonymizes instead of
    /// deleting when a customer has orders (see Customer.Anonymize()); this migration adds the
    /// constraint itself as a defense-in-depth backstop — Restrict (ON DELETE NO ACTION) so the
    /// database refuses a raw delete rather than silently orphaning the row, if that check is
    /// ever bypassed.
    /// </summary>
    public partial class AddOrdersCustomerForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_CustomerId",
                table: "orders",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_CustomerId",
                table: "orders",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_CustomerId",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_CustomerId",
                table: "orders");
        }
    }
}
