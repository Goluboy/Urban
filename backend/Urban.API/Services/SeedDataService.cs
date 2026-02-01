using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Urban.Persistence;

namespace Urban.API.Services;

public static class SeedDataService
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Create roles if they don't exist
        await CreateRolesAsync(roleManager);

        // Create admin user if doesn't exist
        await CreateAdminUserAsync(userManager);
    }

    private static async Task CreateRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "User", "Manager" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task CreateAdminUserAsync(UserManager<IdentityUser> userManager)
    {
        string adminEmail = "admin@example.com";
        string adminPassword = "Admin@123";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(adminUser, adminPassword);
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}