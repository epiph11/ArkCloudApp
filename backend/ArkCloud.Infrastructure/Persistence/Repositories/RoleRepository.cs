using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly ArkCloudDbContext _context;

    public RoleRepository(ArkCloudDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
}
