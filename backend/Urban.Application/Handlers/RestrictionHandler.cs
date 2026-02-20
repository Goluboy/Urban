using NetTopologySuite.Geometries;
using Urban.Application.Interfaces;
using Urban.Domain.Geometry;

namespace Urban.Application.Handlers;

public class RestrictionHandler(IGeoFeatureRepository geoFeatureRepository)
{
    // Distance thresholds for each restriction type (in meters)
    private static readonly Dictionary<string, double> DistanceThresholds = new()
    {
        { "ОКН", 200.0 },
        { "Территория ОКН", 100.0 },
        { "Охранная зона", 50.0 },
        { "Защитная зона", 20.0 },
        { "Здания", 20.0 }
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