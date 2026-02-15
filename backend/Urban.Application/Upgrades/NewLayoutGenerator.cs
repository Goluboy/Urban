using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Logging;
using Urban.Application.Logging.Interfaces;
using Urban.Application.Services;
using Urban.Domain.Geometry;

namespace Urban.Application.Upgrades
{
    public class NewLayoutGenerator(BuildingGenerator buildingGenerator, LayoutManager layoutManager, IGeoLogger geoLogger, LayoutVisualizer visualizer)
    {
        public const double FloorHeight = 3.2;


        private static readonly LayoutRestrictions.RestrictionType[] EnabledRestrictions = new[]
        {
            LayoutRestrictions.RestrictionType.CulturalHeritage_site,
            LayoutRestrictions.RestrictionType.CulturalHeritage_area,
            LayoutRestrictions.RestrictionType.Protection_zone,
            LayoutRestrictions.RestrictionType.Buffer_zone,
            LayoutRestrictions.RestrictionType.Buildings,

            LayoutRestrictions.RestrictionType.Roads
        };

        public BlockLayout[] GenerateLayouts(Polygon plot,
            int? maxFloors = null,
            double? grossFloorArea = null)
        {
            /* TODO

                ADD PARAMETERS FOR AREA GENERATION
                BUILDING GENERATOR MUST NOW WORK ON GRID. right now it generates buildings in blocks. Need a new layer.
                ADD LIST OF POLYGONS INSTEAD OF JUST ONE POLYGON FOR BUILDING

            */

            using (TimeLogger.Measure("GenerateLayouts"))
            {
                // STEP 0: Show original plot
                geoLogger.LogSvg("Step 0: Original plot",
                    ((object)plot, "stroke='green' stroke-width='2' fill='none'"));

                var centroidPoint = new Point(plot.Centroid.Coordinate);
                var centroid = (Point)CoordinatesConverter.FromUtm(centroidPoint, 41, true);
                var insolationCalculator = new InsolationCalculator(centroid.Coordinate.Y, centroid.Coordinate.X);
                const int effectiveMaxFloors = 9;

                // Get restrictions
                var restrictions = LayoutRestrictions.GetRestrictionsWithinDistance(plot, EnabledRestrictions).ToArray();

                // Show restrictions if any
                if (restrictions.Any())
                {
                    var restrictionPolygons = restrictions
                        .Where(r => r.Geometry is Polygon)
                        .Select(r => r.Geometry as Polygon)
                        .ToArray();

                    var logItems = new List<(object geo, string style)>
                    {
                        (plot, "stroke='green' stroke-width='1' fill='none'")
                    };

                    foreach (var restriction in restrictionPolygons)
                    {
                        logItems.Add((restriction, "fill='red' stroke='none' opacity='0.5'")!);
                    }

                    geoLogger.LogSvg("Restrictions on plot", logItems.ToArray());
                }

                // STEP 1: Create grid
                var grid = new Grid(plot, 10.0);
                geoLogger.LogMessage($"Created grid: {grid.XCells}x{grid.YCells} cells ({grid.CellSize}m each)");

                // STEP 2: Mark plot area as available
                grid.MarkAvailableArea(plot);
                geoLogger.LogMessage($"Marked plot area: {grid.GetAvailableCellCount()} available cells");
                visualizer.VisualizeAvailableCells(grid, plot, "Step 2: Plot area marked as available");

                // STEP 3: Subtract restrictions
                if (restrictions.Any())
                {
                    var restrictionPolygons = restrictions
                        .Where(r => r.Geometry is Polygon)
                        .Select(r => r.Geometry as Polygon)
                        .ToList();

                    grid.SubtractRestrictions(restrictionPolygons);
                    geoLogger.LogMessage($"Subtracted restrictions: {grid.GetAvailableCellCount()} available cells remaining");
                    visualizer.VisualizeGridAfterRestrictions(grid, plot, "Step 3: After subtracting restrictions");
                }

                // STEP 4: Clusterize cells (4-directional)
                var clusters = grid.Clusterize4Directional();
                geoLogger.LogMessage($"Found {clusters.Count} clusters using 4-directional connectivity");

                // Visualize colored clusters
                visualizer.VisualizeColoredClusters(grid, "Step 4: Clusters (colored by connectivity)");

                // STEP 6: Subdivide into larger blocks
                var subdividedBlocks = grid.SubdivideClustersIntoLargerBlocks(12000, 30000, 2.0, 1.2);
                geoLogger.LogMessage($"Subdivided into {subdividedBlocks.Count} larger blocks");
                visualizer.VisualizeSubdividedBlocks(grid, plot, subdividedBlocks, "Step 6: Subdivided into larger blocks");

                // STEP 6: Subdivide large clusters into optimal-sized blocks (6400-14400 m²)
                /*
                subdividedBlocks = grid.SubdivideClustersByCells(6400, 14400, 3.0);
                geoLogger.LogMessage($"Subdivided into {subdividedBlocks.Count} optimal blocks (6400-14400 m²)");
                visualizer.VisualizeSubdividedBlocks(grid, plot, subdividedBlocks, "Step 6: Subdivided optimal blocks");
                */

                var layouts = new List<BlockLayout>();
                var random = new Random(1);
                int number = 0;

                foreach (var projPairFactor in new[] { 0.6, 1.0, 1.4 })
                {
                    using (TimeLogger.Measure($"UrbanBlocksLayout({projPairFactor})"))
                    {
                        var (entryPoints, streets, _) = StreetGenerator.SplitPolygonByStreets(plot, projPairFactor);

                        var parks = new List<Polygon>();
                        var sections = new List<Section>();
                        var displayData = new List<BuildingDisplayData>(); // New display data list

                        // Process each SUBDIVIDED block
                        foreach (var block in subdividedBlocks)
                        {
                            // Buffer for setbacks (6m on all sides)
                            var bufferedBlock = block.Buffer(-6) as Polygon;
                            if (bufferedBlock == null || bufferedBlock.Area < 100) continue;

                            // Randomly make some blocks into parks (small blocks or random chance)
                            if (bufferedBlock.Area < 500 || random.Next(10) == 0)
                            {
                                parks.Add(bufferedBlock);
                                continue;
                            }

                            // Generate buildings using BuildingGenerator with ref displayData
                            var blockDisplayData = buildingGenerator.GenerateBuildingsInBlock(
                                bufferedBlock, grid, effectiveMaxFloors, random, ref displayData);

                            // Convert display data back to sections for LayoutManager
                            var blockSections = blockDisplayData.Select(d => new Section
                            {
                                Polygon = d.Polygon,
                                MinFloors = effectiveMaxFloors / 3,
                                MaxFloors = effectiveMaxFloors,
                                Floors = d.Floors
                            }).ToArray();

                            sections.AddRange(blockSections);
                        }

                        if (sections.Count == 0) continue;

                        // Adjust floor counts using LayoutManager
                        layoutManager.FillSectionsFloors(sections.ToArray(), grossFloorArea);

                        // Update the display data with final floor counts
                        foreach (var section in sections)
                        {
                            var displayItem = displayData.FirstOrDefault(d => d.Polygon.Equals(section.Polygon));
                            if (displayItem != null)
                            {
                                displayItem.Floors = section.Floors;
                            }
                        }

                        // Create the final layout using LayoutManager
                        var layout = layoutManager.CreateLayout(
                            $"Grid Layout {number++}", plot, streets, sections, parks, insolationCalculator);
                        layouts.Add(layout);

                        // Pass displayData to visualizer if needed
                        visualizer.VisualizeLayout(layout, number, "Step 7: Building layout option", displayData);
                    }
                }

                geoLogger.LogMessage(TimeLogger.GetTimeInfo().ToString());
                geoLogger.LogMessage($"Generated {layouts.Count} layout variations");

                return layouts.Where(l => l.Sections.Any()).ToArray();
            }
        }
    }
}