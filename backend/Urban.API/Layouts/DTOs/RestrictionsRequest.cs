using System.ComponentModel;
using System.Text.Json;

namespace Urban.API.Layouts.DTOs;

public class RestrictionsRequest
{
    [DefaultValue(@"{""type"":""Polygon"",""coordinates"":[[[60.579,56.811],[60.581,56.812],[60.583,56.811],[60.582,56.810],[60.579,56.811]]]}")]
    public JsonElement Plot { get; set; }
}