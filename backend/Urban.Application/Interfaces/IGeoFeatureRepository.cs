using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Urban.Domain.Common;
using Urban.Domain.Geometry;
using Urban.Domain.Geometry.Data;

namespace Urban.Application.Interfaces;

public interface IGeoFeatureRepository
{
    public Task<List<Restriction>> GetRestrictionsByType(string type, CancellationToken ct = default);
    public Task<List<Restriction>> GetRestrictionsByType(RestrictionType type, CancellationToken ct = default);
    public Task<IList<Restriction>> GetNearestRestrictions(Geometry geometry, string restrictionType, double distanceThreshold, CancellationToken ct = default);
    public Task<int> BulkInsertAsync(string geoJson, string type, CancellationToken ct = default);
    Task ImportFromFileAsync(IFormFile file, string type, CancellationToken ct = default);
}