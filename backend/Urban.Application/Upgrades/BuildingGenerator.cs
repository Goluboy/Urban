using NetTopologySuite.Geometries;
using Urban.Application.Services;

namespace Urban.Application.Upgrades
{
    public class BuildingGenerator
    {
        private static readonly Random _random = new();

        public List<BuildingDisplayData> GenerateBuildingsInBlock(
            Polygon block,
            Grid grid,
            int maxFloors,
            Random random,
            ref List<BuildingDisplayData> displayData)
        {
            var sections = new List<Section>();
            var env = block.EnvelopeInternal;
            var width = env.MaxX - env.MinX;
            var height = env.MaxY - env.MinY;

            if (width < 30 || height < 30)
            {
                // Small block - single building
                double size = Math.Min(width, height) - 4;
                if (size > 1)
                {
                    AddSingleBuilding(block, sections, maxFloors, random, grid, size);
                }
            }
            else
            {
                // Larger block - multiple buildings
                double sectionSize = random.Next(2) == 0 ? 24 : 32;

                // Create cell grid
                var (cellGrid, cellSize, startX, startY) = CreateCellGrid(block, grid, sectionSize);

                // Place buildings using templates
                var placedBuildings = PlaceBuildingTemplates(cellGrid, random);

                // Create building polygons from placed templates
                foreach (var (template, col, row) in placedBuildings)
                {
                    var buildingPolygon = template.GetPolygon(col, row, cellSize, startX, startY);
                    if (buildingPolygon != null &&
                        block.Contains(buildingPolygon) &&
                        grid.CanPlacePolygon(buildingPolygon, 1.0))
                    {
                        sections.Add(new Section
                        {
                            Polygon = buildingPolygon,
                            MinFloors = maxFloors / 3,
                            MaxFloors = maxFloors
                        });
                    }
                }
            }

            // Convert sections to BuildingDisplayData and add to the ref list
            var displayList = new List<BuildingDisplayData>();
            foreach (var section in sections)
            {
                var displayItem = new BuildingDisplayData
                {
                    Polygon = section.Polygon,
                    // Set a default floor count (will be updated by LayoutManager later)
                    Floors = random.Next(section.MinFloors, section.MaxFloors + 1)
                };
                displayList.Add(displayItem);
                displayData?.Add(displayItem);
            }

            return displayList;
        }

        #region Cell Grid Creation

        private (Cell[,] cellGrid, double cellSize, double startX, double startY)
            CreateCellGrid(Polygon block, Grid grid, double buildingSize)
        {
            var env = block.EnvelopeInternal;

            double spacing = 4;
            double cellSize = buildingSize + spacing;
            int cols = (int)Math.Floor((env.MaxX - env.MinX) / cellSize);
            int rows = (int)Math.Floor((env.MaxY - env.MinY) / cellSize);

            if (cols <= 0 || rows <= 0)
            {
                return (new Cell[0, 0], cellSize, 0, 0);
            }

            // Center the grid
            double totalWidth = cols * cellSize;
            double totalHeight = rows * cellSize;
            double startX = env.MinX + (env.Width - totalWidth) / 2;
            double startY = env.MinY + (env.Height - totalHeight) / 2;

            var cellGrid = new Cell[cols, rows];

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    double cellX = startX + c * cellSize;
                    double cellY = startY + r * cellSize;

                    double buildingX = cellX + spacing / 2;
                    double buildingY = cellY + spacing / 2;

                    var testBuilding = CreateSquarePolygon(buildingX, buildingY, buildingSize - spacing);

                    bool isAvailable = block.Contains(testBuilding) && grid.CanPlacePolygon(testBuilding, 1.0);

                    cellGrid[c, r] = new Cell
                    {
                        Col = c,
                        Row = r,
                        IsAvailable = isAvailable,
                        IsAssigned = false
                    };
                }
            }

            return (cellGrid, cellSize, startX, startY);
        }

        #endregion

        #region Template Placement Algorithm

        private List<(BuildingTemplate template, int col, int row)> PlaceBuildingTemplates(
            Cell[,] cellGrid, Random random)
        {
            var placedBuildings = new List<(BuildingTemplate, int, int)>();
            int cols = cellGrid.GetLength(0);
            int rows = cellGrid.GetLength(1);
            int groupId = 0;

            // Get all templates
            var allTemplates = BuildingTemplates.GetAllTemplates();

            // Scan grid for available positions
            for (int col = 0; col < cols; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    if (!cellGrid[col, row].IsAvailable || cellGrid[col, row].IsAssigned)
                        continue;

                    // Find templates that can be placed here
                    var possibleTemplates = allTemplates
                        .Where(t => t.CanPlace(col, row, cellGrid))
                        .ToList();

                    if (possibleTemplates.Count == 0)
                        continue;

                    // Randomly select a template (weighted by size - prefer larger buildings)
                    var selectedTemplate = SelectTemplateWeighted(possibleTemplates, random);

                    // Place the template
                    selectedTemplate.Place(col, row, cellGrid, groupId);
                    placedBuildings.Add((selectedTemplate, col, row));
                    groupId++;
                }
            }

            return placedBuildings;
        }

        private BuildingTemplate SelectTemplateWeighted(List<BuildingTemplate> templates, Random random)
        {
            // Weight by cell count (prefer larger buildings)
            var weightedList = new List<BuildingTemplate>();
            foreach (var template in templates)
            {
                int weight = template.Cells.Length;
                for (int i = 0; i < weight; i++)
                {
                    weightedList.Add(template);
                }
            }

            // Random selection from weighted list
            int index = random.Next(weightedList.Count);
            return weightedList[index];
        }

        #endregion

        #region Helper Methods

        private void AddSingleBuilding(Polygon block, List<Section> sections, int maxFloors, Random random, Grid grid, double size)
        {
            var env = block.EnvelopeInternal;
            double centerX = (env.MinX + env.MaxX) / 2;
            double centerY = (env.MinY + env.MaxY) / 2;

            double x = centerX - size / 2;
            double y = centerY - size / 2;

            var building = CreateSquarePolygon(x, y, size);

            if (block.Contains(building) && grid.CanPlacePolygon(building, 1.0))
            {
                sections.Add(new Section
                {
                    Polygon = building,
                    MinFloors = maxFloors / 3,
                    MaxFloors = maxFloors
                });
            }
        }

        private Polygon CreateSquarePolygon(double x, double y, double size)
        {
            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + size, y),
                new Coordinate(x + size, y + size),
                new Coordinate(x, y + size),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        #endregion
    }

    public class Cell
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsAssigned { get; set; }
        public int GroupId { get; set; } = -1;
    }
}