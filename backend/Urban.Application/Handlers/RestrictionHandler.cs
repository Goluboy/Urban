using NetTopologySuite.Geometries;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Handlers;

public class RestrictionHandler(IGeoFeatureRepository geoFeatureRepository)
{
    // Distance thresholds for each restriction type (in meters)
    private static readonly Dictionary<RestrictionType, double> DistanceThresholds = new()
    {
        { RestrictionType.CulturalHeritage_site, 200.0 },  // ОКН
        { RestrictionType.CulturalHeritage_area, 100.0 },  // Территория ОКН
        { RestrictionType.Protection_zone,        50.0 },  // Охранная зона
        { RestrictionType.Buffer_zone,            20.0 },  // Защитная зона
        { RestrictionType.Buildings,              2.0 },
        { RestrictionType.Roads,                  1.0 }
    };

    public async Task<IList<Restriction>> GetNearestRestrictions(Geometry geometry, CancellationToken ct = default)
    {
        var allRestrictions = new List<Restriction>();

        foreach (var restrictionType in DistanceThresholds.Keys)
        {
            var distanceThreshold = DistanceThresholds[restrictionType];
            var nearbyRestrictions = await geoFeatureRepository.GetNearestRestrictions(geometry, restrictionType, distanceThreshold, ct);
            if (nearbyRestrictions.Count > 0)
                allRestrictions.AddRange(nearbyRestrictions);
        }

        return allRestrictions;
    }
}