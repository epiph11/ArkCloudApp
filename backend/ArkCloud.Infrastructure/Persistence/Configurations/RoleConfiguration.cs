using ArkCloud.Domain.Common;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArkCloud.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Seed data lives on the model itself (not just in the AddAuthentication migration)
        // so it is also applied when a database is built via EnsureCreated (integration tests),
        // not only via `dotnet ef database update`. Same fixed IDs as the migration's InsertData.
        builder.HasData(
            new { Id = new Guid("11111111-0000-0000-0000-000000000001"), Name = RoleNames.Admin },
            new { Id = new Guid("11111111-0000-0000-0000-000000000002"), Name = RoleNames.Manager },
            new { Id = new Guid("11111111-0000-0000-0000-000000000003"), Name = RoleNames.User });

        builder.HasMany(x => x.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.UserRoles)
            .HasField("_userRoles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
