using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.Entities;

public class Role : BaseEntity
{
    private readonly List<UserRole> _userRoles = [];

    public string Name { get; private set; }
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    private Role()
    {
        Name = default!;
    }

    private Role(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Role name is required.");

        Name = name.Trim();
    }

    public static Role Create(string name) => new(name);
}
