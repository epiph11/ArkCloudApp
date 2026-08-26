using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArkCloud.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        // Composite key. The two relationships (-> User, -> Role) are fully configured
        // from the "one" side in UserConfiguration and RoleConfiguration.
        builder.HasKey(x => new { x.UserId, x.RoleId });
    }
}
