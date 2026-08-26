using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkCloud.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ArkCloudDbContext _context;

    public UserRepository(ArkCloudDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        => await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Users.AnyAsync(u => u.Email == Normalize(email), cancellationToken);

    // AsSplitQuery() on all three: loading two sibling collections (UserRoles and
    // RefreshTokens) off the same root via a single query produces a cartesian-product JOIN
    // (one row per combination of role x refresh token). Besides being wasteful, this shape
    // has a known EF Core correctness pitfall where change tracking on a subsequent
    // SaveChanges can misidentify a brand-new child entity as an existing one to UPDATE,
    // which is exactly what caused login to throw DbUpdateConcurrencyException when adding a
    // new refresh token. Splitting into one query per collection avoids the cartesian product
    // entirely.
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Email == Normalize(email), cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        => await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.RefreshTokens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken), cancellationToken);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
