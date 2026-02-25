using System.Text.Json;
using NetTopologySuite.Features;
using Urban.Domain.Common;
using Urban.Domain.Geometry.Data.ValueObjects;

namespace Urban.Domain.Geometry.Data;

public class Restriction : GeoFeature
{
    public Restriction() : base()
    {
        
    }

    public Restriction(IFeature feature) : base(feature)
    {
    }

    public RenderOptions? Options { get; set; }
    public RestrictionType Discriminator { get; set; }
}