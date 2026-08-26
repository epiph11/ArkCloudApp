using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ArkCloud.Infrastructure.Authentication;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher&lt;TUser&gt; (PBKDF2, per-password salt,
/// versioned format so the work factor can be upgraded later without breaking existing hashes).
/// Never store or log plaintext passwords.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _hasher = new();

    public string HashPassword(string password)
        => _hasher.HashPassword(default!, password);

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(default!, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
