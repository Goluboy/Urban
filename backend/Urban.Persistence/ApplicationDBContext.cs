using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;
using Urban.Persistence.Conventions;

namespace Urban.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Restriction> Restrictions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Restriction>(entity =>
        {
            {
                entity
                .Property("Properties")
                .HasColumnType("jsonb");

                entity
                    .Property("Options")
                    .HasColumnType("jsonb");

                entity.Property(r => r.Discriminator)
                    .HasConversion<string>()
                    .HasColumnType("text")
                    .IsRequired();
            }
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new BaseEntityConvention());
        configurationBuilder.Conventions.Add(_ => new GeoFeatureConversation());
    }

}