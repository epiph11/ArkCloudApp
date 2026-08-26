using ArkCloud.Domain.Entities;

namespace ArkCloud.Application.Interfaces;

public interface ITokenService
{
    DateTime AccessTokenExpiration { get; }
    DateTime RefreshTokenExpiration { get; }

    string GenerateAccessToken(User user, IEnumerable<string> roles);
    string GenerateRefreshToken();
}
