using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string Token { get; private set; }
    public DateTime Expiration { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid UserId { get; private set; }

    public bool IsActive => !Revoked && Expiration > DateTime.UtcNow;

    private RefreshToken()
    {
        Token = default!;
    }

    private RefreshToken(string token, DateTime expiration, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new DomainException("Refresh token value is required.");
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");

        Token = token;
        Expiration = expiration;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        Revoked = false;
    }

    public static RefreshToken Create(string token, DateTime expiration, Guid userId)
        => new(token, expiration, userId);

    public void Revoke() => Revoked = true;
}
