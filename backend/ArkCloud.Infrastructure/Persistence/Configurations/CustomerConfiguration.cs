using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArkCloud.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.OwnsOne(x => x.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();

            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("street").HasMaxLength(200).IsRequired();
            address.Property(a => a.City).HasColumnName("city").HasMaxLength(100).IsRequired();
            address.Property(a => a.Country).HasColumnName("country").HasMaxLength(100).IsRequired();
        });
    }
}
