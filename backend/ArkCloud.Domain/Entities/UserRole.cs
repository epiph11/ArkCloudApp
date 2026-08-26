namespace ArkCloud.Domain.Entities;

/// <summary>
/// Explicit join entity for the User &lt;-&gt; Role many-to-many relationship.
/// Kept explicit (rather than an EF Core skip-navigation) so the assignment can
/// carry future metadata (e.g. AssignedAt, AssignedBy) without a breaking change.
/// </summary>
public class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    public User User { get; private set; } = default!;
    public Role Role { get; private set; } = default!;

    private UserRole()
    {
    }

    private UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public static UserRole Create(Guid userId, Guid roleId) => new(userId, roleId);
}
