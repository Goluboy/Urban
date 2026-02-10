using System.Globalization;
using System.Text;
using NetTopologySuite.Geometries;

namespace Urban.Application.Logging;

public class SvgTools
{
	public static string ComposeSvgString(int width, int height, params (object geo, string style)[] layers)
	{
		const int margin = 5;
		IEnumerable<Geometry> getGeometries(object obj) => obj is Geometry geo ? new [] {geo} : obj is IEnumerable<Geometry> e ? e : throw new Exception("Geometry or collection expected");
		
		var allGeometries = layers.SelectMany(i => getGeometries(i.geo));
		
		var allCoords = allGeometries.SelectMany(l => l.Coordinates);

		var x0 = allCoords.Min(c => c.X);
		var y0 = allCoords.Min(c => c.Y);
		var x1 = allCoords.Max(c => c.X);
		var y1 = allCoords.Max(c => c.Y);

		// Shrink the drawing area by 2*margin
		var drawWidth = width - 2 * margin;
		var drawHeight = height - 2 * margin;

		var scale = Math.Min(drawWidth / (x1 - x0), drawHeight / (y1 - y0));

		// Offset all coordinates by margin
		string mapX(double x) => (margin + (x - x0) * scale).ToString(CultureInfo.InvariantCulture);
		string mapY(double y) => (height - margin - (y - y0) * scale).ToString(CultureInfo.InvariantCulture);

		var builder = new StringBuilder();

		builder.Append($"<svg width='{width.ToString(CultureInfo.InvariantCulture)}px' height='{height.ToString(CultureInfo.InvariantCulture)}px'>");

		foreach (var layer in layers)
		{			
			var style = layer.style;
			var geometries = getGeometries(layer.geo).ToArray();
			
			if (string.IsNullOrEmpty(style))
				style = "stroke='red' stroke-width='5'";

			for (int i = 0; i < geometries.Length; i++)
			{
				var geom = geometries[i];
				//var style = styles[i >= styles.Length ? styles.Length - 1 : i];

				if (geom is Point pt)
				{
					builder.Append($"<circle cx='{mapX(pt.X)}' cy='{mapY(pt.Y)}' r='3' {style} />");
				}
				else
				{
					var geometryType = geom is LineString ? "polyline" : geom is Polygon ? "polygon" : throw new Exception("Unsupported geometry type");
					var points = string.Join(" ", geom.Coordinates.Select(c => $" {mapX(c.X)},{mapY(c.Y)}").ToArray());
					builder.Append($"<{geometryType} points='{points}' {style} fill='transparent' />");
				}

				//if (labels != null && labels.Length > i)
				//	svg += $"<text x='{mapX(geom.Centroid.X)}' y='{mapY(geom.Centroid.Y)}' font-size='9' text-anchor='middle' dominant-baseline='middle' dy='.35em' >{labels[i]}</text>";
			}
		}

		return builder.Append("</svg>").ToString();
	}
 
}