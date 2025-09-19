using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Identity;
using ServConnect.Models;

public class RoleSeederMiddleware
{
    private readonly RequestDelegate _next;

    public RoleSeederMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        RoleManager<MongoIdentityRole> roleManager)
    {
        foreach (var role in new[] { RoleTypes.User, RoleTypes.Admin, RoleTypes.ServiceProvider, RoleTypes.Vendor })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new MongoIdentityRole(role));
            }
        }

        await _next(context);
    }
}