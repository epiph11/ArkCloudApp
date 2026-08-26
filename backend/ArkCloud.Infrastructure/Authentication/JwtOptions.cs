namespace ArkCloud.Infrastructure.Authentication;

/// <summary>Bound from the "Jwt" configuration section (appsettings.json / user-secrets / Key Vault).</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
