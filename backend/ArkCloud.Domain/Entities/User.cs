using ArkCloud.Domain.Common;
using ArkCloud.Domain.Exceptions;

namespace ArkCloud.Domain.Entities;

public class User : BaseEntity
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedOutUntil { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    public bool IsLockedOut => LockedOutUntil.HasValue && LockedOutUntil.Value > DateTime.UtcNow;

    private User()
    {
        Email = default!;
        PasswordHash = default!;
        FirstName = default!;
        LastName = default!;
    }

    private User(string email, string passwordHash, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email is required.");
        if (!email.Contains('@'))
            throw new DomainException("Email format is invalid.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
    }

    /// <summary>Registers a new user. Business entity creation only — hashing happens in the caller (Application/Infrastructure).</summary>
    public static User Register(string email, string passwordHash, string firstName, string lastName)
        => new(email, passwordHash, firstName, lastName);

    public void AssignRole(Role role)
    {
        if (_userRoles.Any(ur => ur.RoleId == role.Id))
            return;

        _userRoles.Add(UserRole.Create(Id, role.Id));
    }

    public void AddRefreshToken(RefreshToken token) => _refreshTokens.Add(token);

    public void RevokeRefreshToken(string token)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.Token == token);
        existing?.Revoke();
    }

    /// <summary>Anti-brute-force: increments the failed attempt counter and locks the account once the threshold is reached.</summary>
    public void RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockedOutUntil = DateTime.UtcNow.Add(lockoutDuration);
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
    }

    public void Deactivate() => IsActive = false;
}
