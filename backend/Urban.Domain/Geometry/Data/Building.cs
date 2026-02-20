using NetTopologySuite.Features;
using Urban.Domain.Common;

namespace Urban.Domain.Geometry.Data;

public class Building(IFeature feature) : GeoFeature(feature)
{
}