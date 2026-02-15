using Microsoft.AspNetCore.Http;
using Urban.Application.Services;
using Urban.Domain.Common;
using Urban.Domain.Geometry;

namespace Urban.Persistence.GeoJson.Interfaces;

public interface IGeoFeatureRepository
{
    public Task<Restriction> GetNearestRestrictions(double distanceThreshold, CancellationToken ct = default);
    public Task<int> BulkInsertAsync(string geoJson, CancellationToken ct = default);
    Task ImportFromFileAsync(IFormFile file, CancellationToken ct = default);
}