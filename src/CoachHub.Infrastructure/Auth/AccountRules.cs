using CoachHub.Application.Common.Exceptions;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;

namespace CoachHub.Infrastructure.Auth;

internal static class AccountRules
{
    /// <summary>The login name: the explicit user name, otherwise the email address (§2).</summary>
    public static string UserName(string? userName, string email) =>
        string.IsNullOrWhiteSpace(userName) ? email.Trim() : userName.Trim();

    /// <summary>Identity failures (duplicate email, weak password, ...) are caller errors, not server errors.</summary>
    public static void Ensure(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new ValidationException(result.Errors
            .GroupBy(error => Field(error.Code))
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
    }

    public static async Task EnsureRoleAsync(RoleManager<Role> roles, string role)
    {
        if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new Role { Name = role }));
    }

    private static string Field(string code) => code switch
    {
        _ when code.Contains("Password", StringComparison.Ordinal) => "password",
        _ when code.Contains("Email", StringComparison.Ordinal) => "email",
        _ when code.Contains("UserName", StringComparison.Ordinal) => "userName",
        _ => "account"
    };
}
