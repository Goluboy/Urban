using NetTopologySuite.Geometries;
using Urban.Application.Interfaces;
using Urban.Domain.Geometry;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Handlers;

public class RestrictionHandler(IGeoFeatureRepository geoFeatureRepository)
{
    // Distance thresholds for each restriction type (in meters)
    private static readonly Dictionary<string, double> DistanceThresholds = new()
    {
        { "Restriction", 200.0 },
        { "HeritageBorder", 100.0 },
        { "HeritageZone", 50.0 },
        { "ProtectionZone", 20.0 },
        { "Buildings", 20.0 }
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