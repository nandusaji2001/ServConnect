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

            // Create or update default test user for Selenium automation
            await CreateOrUpdateDefaultTestUser();
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

        private async Task CreateOrUpdateDefaultTestUser()
        {
            const string testEmail = "user@example.com";
            const string testPassword = "Test@123";
            const string testUserName = "Automation Test User";

            var testUser = await _userManager.FindByEmailAsync(testEmail);
            if (testUser == null)
            {
                testUser = new Users
                {
                    UserName = testEmail,
                    Email = testEmail,
                    FullName = testUserName,
                    EmailConfirmed = true,
                    IsProfileCompleted = true,
                    IsAdminApproved = true,
                    District = KeralaDistricts.Idukki,
                    Address = "Automation Test Address",
                    PhoneNumber = "+919876543210",
                    ProfileImageUrl = "/images/default-avatar.png",
                    IdentityProofUrl = "/uploads/identity/test-proof.pdf"
                };

                var createResult = await _userManager.CreateAsync(testUser, testPassword);
                if (!createResult.Succeeded)
                {
                    Console.WriteLine($"Failed to create test user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    return;
                }

                Console.WriteLine($"Test user created successfully: {testEmail}");
            }
            else
            {
                var updated = false;

                if (testUser.UserName != testEmail)
                {
                    testUser.UserName = testEmail;
                    updated = true;
                }

                if (testUser.FullName != testUserName)
                {
                    testUser.FullName = testUserName;
                    updated = true;
                }

                if (!testUser.EmailConfirmed)
                {
                    testUser.EmailConfirmed = true;
                    updated = true;
                }

                if (!testUser.IsProfileCompleted)
                {
                    testUser.IsProfileCompleted = true;
                    updated = true;
                }

                if (!testUser.IsAdminApproved)
                {
                    testUser.IsAdminApproved = true;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(testUser.District))
                {
                    testUser.District = KeralaDistricts.Idukki;
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(testUser.Address))
                {
                    testUser.Address = "Automation Test Address";
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(testUser.PhoneNumber))
                {
                    testUser.PhoneNumber = "+919876543210";
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(testUser.ProfileImageUrl))
                {
                    testUser.ProfileImageUrl = "/images/default-avatar.png";
                    updated = true;
                }

                if (string.IsNullOrWhiteSpace(testUser.IdentityProofUrl))
                {
                    testUser.IdentityProofUrl = "/uploads/identity/test-proof.pdf";
                    updated = true;
                }

                if (updated)
                {
                    var updateResult = await _userManager.UpdateAsync(testUser);
                    if (!updateResult.Succeeded)
                    {
                        Console.WriteLine($"Failed to update test user: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                    }
                }
            }

            if (!await _userManager.IsInRoleAsync(testUser, RoleTypes.User))
            {
                await _userManager.AddToRoleAsync(testUser, RoleTypes.User);
            }

            await EnsurePasswordAsync(testUser, testPassword);
            Console.WriteLine($"Test user ensured for automation: {testEmail}");
        }

        private async Task EnsurePasswordAsync(Users user, string expectedPassword)
        {
            if (await _userManager.CheckPasswordAsync(user, expectedPassword))
            {
                return;
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, expectedPassword);
            if (resetResult.Succeeded)
            {
                Console.WriteLine($"Password reset for automation user: {user.Email}");
            }
            else
            {
                Console.WriteLine($"Failed to reset test user password: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}