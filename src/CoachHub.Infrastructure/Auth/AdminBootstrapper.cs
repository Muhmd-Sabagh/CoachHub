using CoachHub.Application.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Auth;

public sealed class AdminBootstrapper(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IOptions<AdminBootstrapOptions> options,
    ILogger<AdminBootstrapper> logger)
{
    private readonly AdminBootstrapOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConfiguration();

        if (!await roleManager.RoleExistsAsync(AuthRoles.Administrator))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new Role { Name = AuthRoles.Administrator }),
                "create the Administrator role");
        }

        var user = await userManager.FindByEmailAsync(_options.Email);
        if (user is null)
        {
            user = await userManager.FindByIdAsync(SeedIdentity.AdministratorUserId.ToString());
        }

        if (user is null)
        {
            user = new User { UserName = _options.Email, Email = _options.Email, EmailConfirmed = true,
                DisplayName = _options.DisplayName, IsActive = true, LockoutEnabled = true };
            EnsureSucceeded(await userManager.CreateAsync(user, _options.Password),
                "create the bootstrap administrator");
        }
        else if (user.Id == SeedIdentity.AdministratorUserId)
        {
            user.UserName = _options.Email;
            user.Email = _options.Email;
            user.EmailConfirmed = true;
            user.DisplayName = _options.DisplayName;
            user.IsActive = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = null;
            EnsureSucceeded(await userManager.UpdateAsync(user), "activate the seeded administrator");
            if (!await userManager.HasPasswordAsync(user))
            {
                EnsureSucceeded(await userManager.AddPasswordAsync(user, _options.Password),
                    "set the seeded administrator password");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Administrator))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, AuthRoles.Administrator),
                "assign the Administrator role");
        }
        logger.LogInformation("Administrator bootstrap completed for user {UserId}.", user.Id);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Email) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.DisplayName))
        {
            throw new InvalidOperationException(
                "Enabled administrator bootstrap requires Email, Password, and DisplayName configuration.");
        }
        if (string.Equals(_options.Email, SeedIdentity.AdministratorEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The disabled seed placeholder email cannot be used as a real administrator email.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException("Unable to " + operation + ": " +
            string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
