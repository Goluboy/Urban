using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Urban.Domain.Common;
using Urban.Domain.Geometry;

namespace Urban.Application.Interfaces;

public interface IGeoFeatureRepository
{
    public Task<List<GeoFeature>> GetGeoFeaturesByType(string type, CancellationToken ct = default);
    public Task<IList<Restriction>> GetNearestRestrictions(Geometry geometry, string restrictionType, double distanceThreshold, CancellationToken ct = default);
    public Task<int> BulkInsertAsync(string geoJson, string type, CancellationToken ct = default);
    Task ImportFromFileAsync(IFormFile file, string type, CancellationToken ct = default);
}