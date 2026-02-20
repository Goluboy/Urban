using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;

namespace Urban.Persistence.Conventions;

public class GeoFeatureConversation : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        var geoFeatureType = typeof(GeoFeature);

        var entityTypes = modelBuilder.Metadata.GetEntityTypes()
            .Where(t => t.ClrType.IsAssignableTo(geoFeatureType));

        foreach (var entityType in entityTypes)
        {
            var geometryProperty = entityType.FindProperty(nameof(GeoFeature.Geometry));
            geometryProperty?.Builder.HasColumnType("geometry"); // PostGIS type

            var propertiesProperty = entityType.FindProperty(nameof(GeoFeature.Properties));
            propertiesProperty?.Builder.HasColumnType("jsonb");
        }
    }
}