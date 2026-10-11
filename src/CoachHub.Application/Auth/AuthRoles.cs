namespace CoachHub.Application.Auth;

public static class AuthRoles
{
    public const string Administrator = "Administrator";
    public const string Coach = "Coach";
    public const string Client = "Client";

    public static readonly string[] All = [Administrator, Coach, Client];
}
