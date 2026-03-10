using NetTopologySuite.Geometries;
using Urban.Application.GeometryLogic;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Handlers
{
    public class RestrictionsHandler(IGeoFeatureRepository geoFeatureRepository)
    {
        // MATCH old OknData thresholds
        private static readonly Dictionary<RestrictionType, double> thresholds = new()
        {
            { RestrictionType.CulturalHeritage_site, 200.0 },  // ОКН
            { RestrictionType.CulturalHeritage_area, 100.0 },  // Территория ОКН
            { RestrictionType.Protection_zone,        50.0 },  // Охранная зона
            { RestrictionType.Buffer_zone,            20.0 },  // Защитная зона
            { RestrictionType.Buildings,              2.0 },   // Здания
            { RestrictionType.Roads,                  1.0 }    // Дороги
        };

        public static readonly Dictionary<RestrictionType, string> FileMap =
            new()
            {
                { RestrictionType.CulturalHeritage_site, "CulturalHeritage_site.json" },
                { RestrictionType.CulturalHeritage_area, "CulturalHeritage_area.json" },
                { RestrictionType.Protection_zone,       "Protection_zone.json" },
                { RestrictionType.Buffer_zone,           "Buffer_zone.json" },
                { RestrictionType.Buildings,             "Buildings.json" },
                { RestrictionType.Roads,                 "Roads.json" }
            };

        public async Task<List<Restriction>> GetRestrictionsWithinDistance(
            Geometry target,
            params RestrictionType[] rTypes)
        {
            if (target == null || rTypes == null || rTypes.Length == 0)
                return new();

            var result = new List<Restriction>();
            foreach (var rType in rTypes)
            {
                if (!thresholds.TryGetValue(rType, out var threshold))
                    continue;

                var itemsFromDB = await GetRestrictionsByTypeFromDb(rType);

                // Use < (NOT <=)
                foreach (var r in itemsFromDB) 
                {
                    var distance = target.Distance(r.Geometry);
                    if (distance < threshold)
                        result.Add(r);
                }
            }

            return result;
        }

        private async Task<List<Restriction>> GetRestrictionsByTypeFromDb(RestrictionType rType)
        {
            var restrictions = await geoFeatureRepository.GetRestrictionsByType(rType);

            foreach (var restriction in restrictions)
            {
                var geom = restriction.Geometry;

                if (geom != null && geom.IsEmpty)
                    continue;

                if (geom != null)
                {
                    SwapXYFilter.SwapCoordinates(geom);

                    if (geom is Polygon p)
                    {
                        var (converted, _) = CoordinatesConverter.ToUtm(p);
                        geom = converted;
                    }
                    else if (geom is Point pt)
                    {
                        var (converted, _) = CoordinatesConverter.ToUtm(pt);
                        geom = converted;
                    }

                    restriction.Geometry = geom;
                }
            }

            return restrictions;
        }
    }
}
