using ArkCloud.Application.DTOs.Auth;
using ArkCloud.Application.Exceptions;
using ArkCloud.Application.Interfaces;
using ArkCloud.Domain.Common;
using ArkCloud.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ArkCloud.Application.Services;

public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            // No {Email} here: at this point there is no user object to attach an id to,
            // and the email itself is personal data — the message is informative enough
            // without it (see docs/rgpd-classification-donnees.md, minimisation des logs).
            _logger.LogWarning("Registration attempt with an email that is already in use.");
            throw new AuthenticationException("An account with this email already exists.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = User.Register(request.Email, passwordHash, request.FirstName, request.LastName);

        var defaultRole = await _roleRepository.GetByNameAsync(RoleNames.User, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Default role '{RoleNames.User}' is not seeded. Run the AddAuthentication migration first.");

        user.AssignRole(defaultRole);

        await _userRepository.AddAsync(user, cancellationToken);

        var response = await BuildAuthResponseAsync(user, [defaultRole.Name], cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} registered.", user.Id);

        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Same exception/message whether the user doesn't exist or the password is wrong,
        // so the API never reveals which emails are registered.
        if (user is null)
        {
            // Same reasoning as the registration case: no user id exists to log instead.
            _logger.LogWarning("Failed login attempt for an unknown email.");
            throw new AuthenticationException("Invalid email or password.");
        }

        if (user.IsLockedOut)
        {
            _logger.LogWarning("Login blocked for temporarily locked account {UserId}.", user.Id);
            throw new AuthenticationException("This account is temporarily locked due to too many failed attempts. Try again later.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for deactivated account {UserId}.", user.Id);
            throw new AuthenticationException("This account has been deactivated.");
        }

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            user.RegisterFailedLogin(MaxFailedAttempts, LockoutDuration);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "User {UserId} failed login (attempt {Attempts}/{MaxAttempts}).",
                user.Id, user.FailedLoginAttempts, MaxFailedAttempts);

            throw new AuthenticationException("Invalid email or password.");
        }

        user.RegisterSuccessfulLogin();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var response = await BuildAuthResponseAsync(user, roles, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in.", user.Id);

        return response;
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var token = user?.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);

        if (user is null || token is null || !token.IsActive)
        {
            _logger.LogWarning("Invalid or expired refresh token presented.");
            throw new AuthenticationException("Invalid or expired refresh token.");
        }

        token.Revoke();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var response = await BuildAuthResponseAsync(user, roles, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Access token refreshed for {UserId}.", user.Id);

        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
            return;

        user.RevokeRefreshToken(refreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged out.", user.Id);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiration = _tokenService.RefreshTokenExpiration;

        var refreshToken = RefreshToken.Create(refreshTokenValue, refreshTokenExpiration, user.Id);

        // Domain-consistent in-memory state (User.RefreshTokens reflects the new token
        // immediately)...
        user.AddRefreshToken(refreshToken);

        // ...and an explicit repository-level Add so EF Core tracks it as Added rather than
        // inferring the state via navigation-collection graph fixup during SaveChanges, which
        // was misidentifying this brand-new token as an existing row to UPDATE.
        await _userRepository.AddRefreshTokenAsync(refreshToken, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            Expires = _tokenService.AccessTokenExpiration,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpires = refreshTokenExpiration,
            Email = user.Email,
            Roles = roles.ToList()
        };
    }
}
