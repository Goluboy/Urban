using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;
using Serilog.Events;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Data.Entity;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urban.API.Auth.Filters;
using Urban.API.Auth.Services;
using Urban.API.Auth.Services.Interfaces;
using Urban.Application.Handlers;
using Urban.Application.Interfaces;
using Urban.Application.Logging;
using Urban.Application.Logging.Interfaces;
using Urban.Application.Services;
using Urban.Application.Upgrades;
using Urban.Persistence;
using Urban.Persistence.GeoJson;

namespace Urban.API;
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);
        });

        builder.Services.AddScoped<IGeoLogger, GeoLogger>();
        builder.Services.AddScoped<RestrictionHandler>();

        builder.Services.AddTransient<NewLayoutGenerator>();
        builder.Services.AddTransient<BuildingGenerator>();
        builder.Services.AddTransient<LayoutManager>();
        builder.Services.AddTransient<LayoutVisualizer>();
        builder.Services.AddTransient<LayoutRestrictions>();

        builder.Services.AddScoped<IGeoFeatureRepository>(sp =>
            new GeoFeatureRepository(
                sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"),
                sp.GetRequiredService<ApplicationDbContext>()
            )
        );

        builder.Services.AddTransient<IJWTService, JwtService>();
        
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
        dataSourceBuilder
            .UseNetTopologySuite()
            .EnableDynamicJson();

        var dataSource = dataSourceBuilder.Build();

        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
            dataSource,
            o => o.UseNetTopologySuite()));

        builder.Services.AddAuth();

        // Add permissive CORS policy
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });

        builder.Services.AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                opts.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
                opts.JsonSerializerOptions.MaxDepth = 64;
            });

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSwaggerGen(

                c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiPlayground", Version = "v1" });
                    c.AddSecurityDefinition(
                        "token",
                        new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.Http,
                            BearerFormat = "JWT",
                            Scheme = "Bearer",
                            In = ParameterLocation.Header,
                            Name = HeaderNames.Authorization
                        }
                    );

                    c.OperationFilter<AuthResponsesOperationFilter>();

                    c.AddSecurityRequirement(
                        new OpenApiSecurityRequirement
                        {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "token"
                                },
                            },
                            Array.Empty<string>()
                        }
                        }
                    );
                }
            );
        }
        
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        app.UseRouting();

        // Enable CORS globally
        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth API v1");
                c.RoutePrefix = string.Empty;

                c.DefaultModelsExpandDepth(-1);
                c.DocExpansion(DocExpansion.None);
            });
        }

        await app.Services.InitializeSeedDataAsync();

        await app.RunAsync();
    }
}