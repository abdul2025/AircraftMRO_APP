using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AircraftMRO.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(ApplicationRoles.Administrator))
        {
            await roleManager.CreateAsync(new ApplicationRole { Name = ApplicationRoles.Administrator });
        }

        var seedEmail = configuration["Identity:SeedAdmin:Email"];
        var seedPassword = configuration["Identity:SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        var existingUser = await userManager.FindByEmailAsync(seedEmail);
        if (existingUser is not null)
        {
            return;
        }

        var adminUser = new ApplicationUser
        {
            UserName = seedEmail,
            Email = seedEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, seedPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, ApplicationRoles.Administrator);
        }
    }
}
