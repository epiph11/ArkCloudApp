using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ArkCloud.Tests.Component.TestSupport;

/// <summary>
/// Builds throwaway JWTs for tests. JwtAuthenticationStateProvider only ever *reads* claims
/// out of the token client-side (see its remarks) — the real signature check happens on the
/// API, so an unsigned token is sufficient here and keeps the test project dependency-free.
/// </summary>
public static class JwtTestTokens
{
    public static string CreateUnsigned(string email, IEnumerable<string>? roles = null, TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email)
        };

        foreach (var role in roles ?? [])
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: "ArkCloud",
            audience: "ArkCloud",
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
