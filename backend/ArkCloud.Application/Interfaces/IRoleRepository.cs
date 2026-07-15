using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
