using NetTopologySuite.Features;

namespace Urban.Persistence.GeoJson.Extensions;

public static class AttributesExtensions
{
    public static Dictionary<string, object> ToDictionary(this IAttributesTable attributes)
    {
        var dict = new Dictionary<string, object>();
        foreach (var name in attributes.GetNames())
        {
            dict[name] = attributes[name] ?? DBNull.Value;
        }
        return dict;
    }
}