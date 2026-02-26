using Urban.Domain.Common;

namespace Urban.Application.Interfaces;

public interface IGeoJsonParser
{
    List<GeoFeature> ParseGeoJson(string geoJson);
    List<GeoFeature> ReadGeoJson(string json);
}