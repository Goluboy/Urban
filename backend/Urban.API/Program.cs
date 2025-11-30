using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Urban.API.Data;

namespace Urban.API;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add EF DbContext (InMemory for development)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("UrbanAuthDb"));

        // Add Identity (users + roles) with EF stores
        builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Add authentication & authorization (Identity registers cookie auth by default)
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization(options =>
        {
            // Example: require authenticated users by default. Remove if you want anonymous endpoints.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // Add controllers
        builder.Services.AddControllers();

        var app = builder.Build();

        // Middleware pipeline
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        // Map controllers
        app.MapControllers();

        app.Run();
    }
}