using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArkCloud.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // RGPD finding (docs/rgpd-classification-donnees.md §3): this FK never existed at the
        // EF Core level, so a hard delete of a Customer with existing orders silently left
        // orders.customer_id orphaned instead of failing. CustomerAppService.DeleteAsync now
        // anonymizes rather than deletes when orders exist (see Customer.Anonymize()), which
        // means this Restrict constraint should never actually fire in the app's own code path
        // — it's a defense-in-depth backstop against any other caller (a script, a future admin
        // tool) that hard-deletes a Customer directly without going through that check.
        // No navigation property added on Customer — orders are looked up via IOrderRepository,
        // not via a Customer.Orders collection, so HasOne(...) without WithMany(x => ...) here.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(x => x.TotalAmount);

        // Order exposes its items through a read-only property backed by a
        // private field. Configure the navigation on the public property, then
        // tell EF to read/write it via the "_items" field instead of trying to
        // map the field as a separate navigation (which caused the clash).
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
