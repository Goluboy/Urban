using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Logging;
using Urban.Application.Logging.Interfaces;
using Urban.Application.Services;

namespace Urban.Application.Upgrades
{
    public class LayoutVisualizer(IGeoLogger geoLogger)
    {
        public void VisualizeColoredClusters(Grid grid, string title)
        {
            var coloredCells = grid.GetColoredClusters4Directional();
            var logItems = new List<(object geo, string style)>();

            var groups = coloredCells.GroupBy(x => x.clusterId);
            foreach (var group in groups)
            {
                string fillColor = GetRainbowColor(group.Key);
                string strokeColor = "#000000";

                foreach (var (polygon, _) in group)
                {
                    logItems.Add(((object)polygon,
                        $"fill='{fillColor}' stroke='{strokeColor}' stroke-width='0.5'"));
                }
            }

            geoLogger.LogSvg(title, logItems.ToArray());
        }

        public void VisualizeUnitedBlocks(Grid grid, Polygon plot, List<Polygon> blocks, string title)
        {
            var logItems = new List<(object geo, string style)>
            {
                ((object)plot, "stroke='green' stroke-width='1' fill='none'")
            };

            for (int i = 0; i < blocks.Count; i++)
            {
                string fillColor = GetRainbowColor(i + 1);
                string strokeColor = "#000000";

                logItems.Add(((object)blocks[i],
                    $"fill='{fillColor}' stroke='{strokeColor}' stroke-width='1.0' opacity='1.0'"));
            }

            geoLogger.LogSvg(title, logItems.ToArray());
        }

        public void VisualizeAvailableCells(Grid grid, Polygon plot, string title)
        {
            var availableCells = grid.GetAvailableCells();
            var restrictedCells = grid.GetRestrictedCells();

            var logItems = new List<(object geo, string style)>
            {
                ((object)plot, "stroke='green' stroke-width='1' fill='none'")
            };

            foreach (var cell in restrictedCells)
            {
                logItems.Add(((object)cell, "fill='#f0f0f0' stroke='#000000' stroke-width='0.3'"));
            }

            foreach (var cell in availableCells)
            {
                logItems.Add(((object)cell, "fill='#00FF00' stroke='#000000' stroke-width='0.3'"));
            }

            geoLogger.LogSvg(title, logItems.ToArray());
        }

        public void VisualizeGridAfterRestrictions(Grid grid, Polygon plot, string title)
        {
            var availableCells = grid.GetAvailableCells();
            var restrictedCells = grid.GetRestrictedCells();

            var logItems = new List<(object geo, string style)>
            {
                ((object)plot, "stroke='green' stroke-width='1' fill='none'")
            };

            foreach (var cell in restrictedCells)
            {
                logItems.Add(((object)cell, "fill='#FF0000' stroke='#000000' stroke-width='0.3'"));
            }

            foreach (var cell in availableCells)
            {
                logItems.Add(((object)cell, "fill='#00FF00' stroke='#000000' stroke-width='0.3'"));
            }

            geoLogger.LogSvg(title, logItems.ToArray());
        }

        public void VisualizeSubdividedBlocks(Grid grid, Polygon plot, List<Polygon> blocks, string title)
        {
            var logItems = new List<(object geo, string style)>
            {
                ((object)plot, "stroke='green' stroke-width='1' fill='none'")
            };

            for (int i = 0; i < blocks.Count; i++)
            {
                string fillColor = GetPastelColor(i + 1);
                string strokeColor = "#000000";

                logItems.Add(((object)blocks[i],
                    $"fill='{fillColor}' stroke='{strokeColor}' stroke-width='0.5' opacity='0.8'"));
            }

            geoLogger.LogSvg(title, logItems.ToArray());
        }

        public void VisualizeLayout(BlockLayout layout, int layoutNumber, string title)
        {
            var sectionPolygons = layout.Sections.Select(s => s.Polygon).ToArray();
            var parkPolygons = layout.Parks ?? Array.Empty<Polygon>();
            var streetLines = layout.Streets ?? Array.Empty<LineString>();

            var logItems = new List<(object geo, string style)>
            {
                ((object)layout.Block, "stroke='green' stroke-width='1' fill='none'")
            };

            foreach (var street in streetLines)
            {
                logItems.Add(((object)street, Styles.Street));
            }

            foreach (var section in layout.Sections)
            {
                float intensity = Math.Min(1.0f, section.Floors / 20.0f);
                int blueValue = (int)(100 + 155 * intensity);
                string buildingColor = $"#0000{blueValue:X2}";

                logItems.Add(((object)section.Polygon,
                    $"fill='{buildingColor}' stroke='#000033' stroke-width='0.5'"));
            }

            foreach (var park in parkPolygons)
            {
                logItems.Add(((object)park, Styles.Parks));
            }

            geoLogger.LogSvg($"{title} {layoutNumber}", logItems.ToArray());
        }
        public void VisualizeLayout(BlockLayout layout, int layoutNumber, string title, List<BuildingDisplayData> displayData = null)
        {
            var sectionPolygons = layout.Sections.Select(s => s.Polygon).ToArray();
            var parkPolygons = layout.Parks ?? Array.Empty<Polygon>();
            var streetLines = layout.Streets ?? Array.Empty<LineString>();

            var logItems = new List<(object geo, string style)>
    {
        ((object)layout.Block, "stroke='green' stroke-width='1' fill='none'")
    };

            foreach (var street in streetLines)
            {
                logItems.Add(((object)street, Styles.Street));
            }

            // Use displayData if provided, otherwise fall back to layout.Sections
            if (displayData != null && displayData.Count > 0)
            {
                // Visualize using BuildingDisplayData
                foreach (var building in displayData)
                {
                    if (building.Polygon == null) continue;

                    float intensity = Math.Min(1.0f, building.Floors / 20.0f);
                    int blueValue = (int)(100 + 155 * intensity);
                    string buildingColor = $"#0000{blueValue:X2}";

                    logItems.Add(((object)building.Polygon,
                        $"fill='{buildingColor}' stroke='#000033' stroke-width='0.5'"));
                }
            }
            else
            {
                // Fall back to original logic using layout.Sections
                foreach (var section in layout.Sections)
                {
                    float intensity = Math.Min(1.0f, section.Floors / 20.0f);
                    int blueValue = (int)(100 + 155 * intensity);
                    string buildingColor = $"#0000{blueValue:X2}";

                    logItems.Add(((object)section.Polygon,
                        $"fill='{buildingColor}' stroke='#000033' stroke-width='0.5'"));
                }
            }

            foreach (var park in parkPolygons)
            {
                logItems.Add(((object)park, Styles.Parks));
            }

            geoLogger.LogSvg($"{title} {layoutNumber}", logItems.ToArray());
        }
        private string GetRainbowColor(int clusterId)
        {
            string[] rainbowColors = {
                "#FF0000", "#FF7F00", "#FFFF00", "#00FF00", "#0000FF",
                "#4B0082", "#9400D3", "#FF00FF", "#00FFFF", "#FF4500",
                "#FFD700", "#ADFF2F", "#1E90FF", "#FF1493", "#7CFC00"
            };

            return rainbowColors[(clusterId - 1) % rainbowColors.Length];
        }

        private string GetPastelColor(int blockId)
        {
            string[] pastelColors = {
                "#FFB3BA", "#FFDFBA", "#FFFFBA", "#BAFFC9", "#BAE1FF",
                "#D0BAFF", "#FFBAF0", "#A3FFBA", "#BAFFE4", "#BAC9FF",
                "#FFD8BA", "#E4BAFF", "#BAFFF0", "#FFBAC9", "#BAE1FF"
            };

            return pastelColors[(blockId - 1) % pastelColors.Length];
        }
    }
}