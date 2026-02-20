using System.Text.Json;
using NetTopologySuite.Features;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data.ValueObjects;

namespace Urban.Domain.Geometry.Data;

public class Heritage : GeoFeature
{
    public Heritage() : base()
    {
        
    }

    public Heritage(IFeature feature) : base(feature)
    {
    }

    public RenderOptions Options { get; set; }
}