using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Identity;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class DatabaseSeeder
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<MongoIdentityRole> _roleManager;

        public DatabaseSeeder(UserManager<Users> userManager, RoleManager<MongoIdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task SeedAsync()
        {
            // Create roles if they don't exist
            await CreateRoleIfNotExists(RoleTypes.Admin);
            await CreateRoleIfNotExists(RoleTypes.User);
            await CreateRoleIfNotExists(RoleTypes.ServiceProvider);
            await CreateRoleIfNotExists(RoleTypes.Vendor);

            // Create default admin user
            await CreateDefaultAdmin();
        }

        private async Task CreateRoleIfNotExists(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new MongoIdentityRole(roleName));
            }
        }

        private async Task CreateDefaultAdmin()
        {
            const string adminEmail = "admin@gmail.com";
            const string adminPassword = "Admin123";

            var existingAdmin = await _userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new Users
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, RoleTypes.Admin);
                    Console.WriteLine($"Admin user created successfully: {adminEmail}");
                }
                else
                {
                    Console.WriteLine($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"Admin user already exists: {adminEmail}");
            }
        }
    }
}