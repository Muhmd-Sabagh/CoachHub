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
        if (!_options.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ValidateConfiguration();

        if (!await roleManager.RoleExistsAsync(AuthRoles.Administrator))
        {
            var roleResult = await roleManager.CreateAsync(new Role
            {
                Name = AuthRoles.Administrator
            });
            EnsureSucceeded(roleResult, "create the Administrator role");
        }

        var user = await userManager.FindByEmailAsync(_options.Email);
        if (user is null)
        {
            user = new User
            {
                UserName = _options.Email,
                Email = _options.Email,
                EmailConfirmed = true,
                DisplayName = _options.DisplayName,
                IsActive = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, _options.Password);
            EnsureSucceeded(createResult, "create the bootstrap administrator");
        }

        if (!await userManager.IsInRoleAsync(user, AuthRoles.Administrator))
        {
            var roleResult = await userManager.AddToRoleAsync(user, AuthRoles.Administrator);
            EnsureSucceeded(roleResult, "assign the Administrator role");
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
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var descriptions = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException("Unable to " + operation + ": " + descriptions);
    }
}
