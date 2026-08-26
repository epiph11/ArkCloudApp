namespace ArkCloud.Application.Exceptions;

/// <summary>
/// Thrown for any authentication failure (unknown user, bad password, locked account,
/// deactivated account, invalid/expired refresh token). Mapped to HTTP 401 by
/// ExceptionHandlingMiddleware. Intentionally uses one message for "unknown email" and
/// "wrong password" so the API never reveals whether an email is registered.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message)
    {
    }
}
