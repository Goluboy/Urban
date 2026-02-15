using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Urban.Domain.Common;
using Urban.Domain.Geometry;

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
            geometryProperty?.Builder.HasColumnType("geometry (Polygon, 4326)"); // PostGIS type

            var propertiesProperty = entityType.FindProperty(nameof(GeoFeature.Properties));
            propertiesProperty?.Builder.HasColumnType("jsonb");

            var addrStreetProperty = entityType.FindProperty(nameof(BuildlingBASE.AddrStreet));

            if (addrStreetProperty != null)
            {
                addrStreetProperty.Builder.HasColumnType("text");
                addrStreetProperty.SetAnnotation(
                    "Relational:ComputedColumnSql",
                    "(properties -> 'addr:street')::text"
                );
                addrStreetProperty.SetAnnotation(
                    "Relational:ComputedColumnSql:Stored",
                    true
                );
            }

            var AddrHouseNumber = entityType.FindProperty(nameof(BuildlingBASE.AddrHouseNumber));

            if (AddrHouseNumber != null)
            {
                AddrHouseNumber.Builder.HasColumnType("text");
                AddrHouseNumber.SetAnnotation(
                    "Relational:ComputedColumnSql",
                    "(properties -> 'addr:housenumber')::text"
                );
                AddrHouseNumber.SetAnnotation(
                    "Relational:ComputedColumnSql:Stored",
                    true
                );
            }
        }
    }
}