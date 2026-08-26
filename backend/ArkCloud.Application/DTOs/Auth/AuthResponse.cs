namespace ArkCloud.Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime Expires { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpires { get; set; }
    public string Email { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
}
