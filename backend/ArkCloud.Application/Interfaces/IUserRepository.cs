using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly tracks a newly-minted refresh token as Added. Relying solely on
    /// User.AddRefreshToken (a plain in-memory list mutation on an already-tracked User's
    /// navigation collection) leaves EF Core to infer Added-vs-Modified via graph fixup during
    /// SaveChanges, which can misidentify a brand-new token as an existing row to UPDATE
    /// (surfaced as DbUpdateConcurrencyException: 0 rows affected). Calling this explicitly
    /// removes the ambiguity.
    /// </summary>
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
