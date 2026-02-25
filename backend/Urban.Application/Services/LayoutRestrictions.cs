using NetTopologySuite.Geometries;
using System.Text.Json;
using Urban.Application.Helpers;
using Urban.Application.Interfaces;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Services
{
    public class LayoutRestrictions(IGeoFeatureRepository geoFeatureRepository)
    {
        // MATCH old OknData thresholds
        private static readonly Dictionary<RestrictionType, double> thresholds = new()
        {
            { RestrictionType.CulturalHeritage_site, 200.0 },  // ОКН
            { RestrictionType.CulturalHeritage_area, 100.0 },  // Территория ОКН
            { RestrictionType.Protection_zone,        50.0 },  // Охранная зона
            { RestrictionType.Buffer_zone,            20.0 },  // Защитная зона
            { RestrictionType.Buildings,              2.0 },
            { RestrictionType.Roads,                  1.0 }
        };

        // -------------------------------------------------------
        // MAIN METHOD - consistent with OknData
        // -------------------------------------------------------
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

                var items = await GetRestrictionsByType(rType);

                // Use < (NOT <=)
                foreach (var r in items) 
                {
                    var distance = target.Distance(r.Geometry);
                    if (distance < threshold)
                        result.Add(r);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Load all restrictions of a type
        // -------------------------------------------------------
        private async Task<List<Restriction>> GetRestrictionsByType(RestrictionType rType)
        {
            return await geoFeatureRepository.GetRestrictionsByType(rType);
        }
    }
}
