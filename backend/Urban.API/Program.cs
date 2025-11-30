using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Urban.Persistence;

namespace Urban.API;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add EF DbContext (InMemory for development)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Add Identity (users + roles) with EF stores
        builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();


        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

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