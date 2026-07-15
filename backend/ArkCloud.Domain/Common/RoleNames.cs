namespace ArkCloud.Domain.Common;

/// <summary>
/// Well-known role names seeded into the database by the AddAuthentication migration.
/// Referenced from the Application layer (default role assignment) and the API layer
/// (authorization policies) so the string values only live in one place.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";
}
