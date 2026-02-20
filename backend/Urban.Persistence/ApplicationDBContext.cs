using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;
using Urban.Persistence.Conventions;

namespace Urban.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<GeoFeature> GeoFeatures { get; set; }
    public DbSet<Heritage> Heritages { get; set; }
    public DbSet<BuildingBASE> BuildingsBase { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GeoFeature>(entity =>
        {
            entity
                .Property("Properties")
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<Heritage>(entity => 
            entity
                .Property("Options")
                .HasColumnType("jsonb")
        );
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new BaseEntityConvention());
        configurationBuilder.Conventions.Add(_ => new GeoFeatureConversation());
    }

}