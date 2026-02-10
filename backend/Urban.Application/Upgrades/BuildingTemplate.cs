using NetTopologySuite.Geometries;

namespace Urban.Application.Upgrades
{
    public enum BuildingCategory
    {
        Residential,
        Education,
        Medical,
        Generic
    }

    public class BuildingTemplate
    {
        public string Name { get; }
        public (int col, int row)[] Cells { get; }
        public BuildingCategory Category { get; }
        public int MinFloors { get; }
        public int MaxFloors { get; }
        public double AreaPerFloor { get; }

        public BuildingTemplate(string name, BuildingCategory category,
            int minFloors, int maxFloors, double areaPerFloor,
            params (int col, int row)[] cells)
        {
            Name = name;
            Category = category;
            Cells = cells;
            MinFloors = minFloors;
            MaxFloors = maxFloors;
            AreaPerFloor = areaPerFloor;
        }

        // Calculate the polygon for this building at a specific position
        public Polygon GetPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double spacing = 10)
        {
            spacing = cellSize * 0.15f;
            double buildingSize = cellSize - spacing;

            switch (Name)
            {
                // SINGLE CELL
                case "Single":
                    return CreateSingleCellPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // HORIZONTAL LINE SHAPES
                case "Line2_H":
                    return CreateLine2HPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "Line3_H":
                    return CreateLine3HPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "Line4_H":
                    return CreateLine4HPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // VERTICAL LINE SHAPES
                case "Line2_V":
                    return CreateLine2VPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "Line3_V":
                    return CreateLine3VPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "Line4_V":
                    return CreateLine4VPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // L-SHAPES (3 cells)
                case "L3_Standard":
                    return CreateL3StandardPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "L3_Mirror":
                    return CreateL3MirrorPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // L-SHAPES (4 cells)
                case "L4_Example":
                    return CreateL4ExamplePolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // L-SHAPES (5 cells)
                case "L5":
                    return CreateL5Polygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // SQUARE SHAPES
                case "Square2x2":
                    return CreateSquare2x2Polygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
                case "Square3x3":
                    return CreateSquare3x3Polygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                // T-SHAPE
                case "T4_Standard":
                    return CreateT4StandardPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);

                default:
                    // Fallback to original method for any new templates
                    return CreateGenericPolygon(startCol, startRow, cellSize, startX, startY, buildingSize, spacing);
            }
        }

        #region Special Case Polygon Methods

        private Polygon CreateSingleCellPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + buildingSize, y),
                new Coordinate(x + buildingSize, y + buildingSize),
                new Coordinate(x, y + buildingSize),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine2HPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Two horizontal cells: (0,0), (1,0)
            double totalWidth = (2 * buildingSize) + spacing;
            double totalHeight = buildingSize;

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine3HPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Three horizontal cells: (0,0), (1,0), (2,0)
            double totalWidth = (3 * buildingSize) + (2 * spacing);
            double totalHeight = buildingSize;

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine4HPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Four horizontal cells: (0,0), (1,0), (2,0), (3,0)
            double totalWidth = (4 * buildingSize) + (3 * spacing);
            double totalHeight = buildingSize;

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine2VPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Two vertical cells: (0,0), (0,1)
            double totalWidth = buildingSize;
            double totalHeight = (2 * buildingSize) + spacing;

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine3VPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Three vertical cells: (0,0), (0,1), (0,2)
            double totalWidth = buildingSize;
            double totalHeight = (3 * buildingSize) + (2 * spacing);

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateLine4VPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Four vertical cells: (0,0), (0,1), (0,2), (0,3)
            double totalWidth = buildingSize;
            double totalHeight = (4 * buildingSize) + (3 * spacing);

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateL3StandardPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // L3 Standard: (0,0), (0,1), (1,0)
            double leftX = startX + startCol * cellSize + spacing / 2;
            double bottomY = startY + startRow * cellSize + spacing / 2;

            // Points in clockwise order
            var coordinates = new List<Coordinate>
            {
                // Bottom-left (cell 0,0)
                new Coordinate(leftX, bottomY),
                // Bottom-right (cell 1,0)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY),
                // Top-right (cell 1,0)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + buildingSize),
                // Move left to cell 0,0 top-right
                new Coordinate(leftX + buildingSize, bottomY + buildingSize),
                // Move up to cell 0,1 top-right
                new Coordinate(leftX + buildingSize, bottomY + (2 * buildingSize) + spacing),
                // Move left to cell 0,1 top-left
                new Coordinate(leftX, bottomY + (2 * buildingSize) + spacing),
                // Back to start
                new Coordinate(leftX, bottomY)
            };

            return new Polygon(new LinearRing(coordinates.ToArray()));
        }

        private Polygon CreateL3MirrorPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // L3 Mirror: (0,0), (1,0), (1,1)
            double leftX = startX + startCol * cellSize + spacing / 2;
            double bottomY = startY + startRow * cellSize + spacing / 2;

            // Points in clockwise order
            var coordinates = new List<Coordinate>
            {
                // Bottom-left (cell 0,0)
                new Coordinate(leftX, bottomY),
                // Bottom-right (cell 1,0)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY),
                // Top-right (cell 1,1)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (2 * buildingSize) + spacing),
                // Move left to cell 1,1 top-left
                new Coordinate(leftX + buildingSize + spacing, bottomY + (2 * buildingSize) + spacing),
                // Move down to cell 1,0 top-left
                new Coordinate(leftX + buildingSize + spacing, bottomY + buildingSize),
                // Move left to cell 0,0 top-right
                new Coordinate(leftX, bottomY + buildingSize),
                // Back to start
                new Coordinate(leftX, bottomY)
            };

            return new Polygon(new LinearRing(coordinates.ToArray()));
        }

        private Polygon CreateSquare2x2Polygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Square 2x2: (0,0), (1,0), (0,1), (1,1)
            double totalWidth = (2 * buildingSize) + spacing;
            double totalHeight = (2 * buildingSize) + spacing;

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateSquare3x3Polygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Square 3x3: 3x3 grid of cells
            double totalWidth = (3 * buildingSize) + (2 * spacing);
            double totalHeight = (3 * buildingSize) + (2 * spacing);

            double x = startX + startCol * cellSize + spacing / 2;
            double y = startY + startRow * cellSize + spacing / 2;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x, y),
                new Coordinate(x + totalWidth, y),
                new Coordinate(x + totalWidth, y + totalHeight),
                new Coordinate(x, y + totalHeight),
                new Coordinate(x, y)
            };
            return new Polygon(new LinearRing(coordinates));
        }

        private Polygon CreateL4ExamplePolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // L4 shape: (0,0), (0,1), (0,2), (1,2)
            double leftX = startX + startCol * cellSize + spacing / 2;
            double bottomY = startY + startRow * cellSize + spacing / 2;

            var coordinates = new List<Coordinate>
            {
                // Bottom-left (cell 0,0)
                new Coordinate(leftX, bottomY),
                // Bottom-right (cell 0,0)
                new Coordinate(leftX + buildingSize, bottomY),
                // Top-right (cell 0,0) / Bottom-right (cell 0,1)
                new Coordinate(leftX + buildingSize, bottomY + buildingSize + spacing),
                // Top-right (cell 0,1) / Bottom-right (cell 0,2)
                new Coordinate(leftX + buildingSize, bottomY + (2 * buildingSize) + (2 * spacing)),
                // Bottom-right (cell 1,2)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (2 * buildingSize) + (2 * spacing)),
                // Top-right (cell 1,2)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Top-left (cell 1,2) / Top-right (cell 0,2)
                new Coordinate(leftX + buildingSize, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Top-left (cell 0,2)
                new Coordinate(leftX, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Bottom-left (cell 0,2) / Top-left (cell 0,1)
                new Coordinate(leftX, bottomY + (2 * buildingSize) + spacing),
                // Bottom-left (cell 0,1) / Top-left (cell 0,0)
                new Coordinate(leftX, bottomY + buildingSize + spacing),
                // Back to start
                new Coordinate(leftX, bottomY)
            };

            return new Polygon(new LinearRing(coordinates.ToArray()));
        }

        private Polygon CreateL5Polygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // L5 shape: (0,0), (0,1), (0,2), (1,2), (2,2) - a bigger L shape
            double leftX = startX + startCol * cellSize + spacing / 2;
            double bottomY = startY + startRow * cellSize + spacing / 2;

            var coordinates = new List<Coordinate>
            {
                // Bottom-left (cell 0,0)
                new Coordinate(leftX, bottomY),
                // Bottom-right (cell 0,0)
                new Coordinate(leftX + buildingSize, bottomY),
                // Top-right (cell 0,0) / Bottom-right (cell 0,1)
                new Coordinate(leftX + buildingSize, bottomY + buildingSize + spacing),
                // Top-right (cell 0,1) / Bottom-right (cell 0,2)
                new Coordinate(leftX + buildingSize, bottomY + (2 * buildingSize) + (2 * spacing)),
                // Bottom-right (cell 1,2)
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (2 * buildingSize) + (2 * spacing)),
                // Bottom-right (cell 2,2)
                new Coordinate(leftX + (3 * buildingSize) + (2 * spacing), bottomY + (2 * buildingSize) + (2 * spacing)),
                // Top-right (cell 2,2)
                new Coordinate(leftX + (3 * buildingSize) + (2 * spacing), bottomY + (3 * buildingSize) + (2 * spacing)),
                // Move left to cell 1,2 top-right
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Move left to cell 0,2 top-right
                new Coordinate(leftX + buildingSize, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Top-left (cell 0,2)
                new Coordinate(leftX, bottomY + (3 * buildingSize) + (2 * spacing)),
                // Bottom-left (cell 0,2) / Top-left (cell 0,1)
                new Coordinate(leftX, bottomY + (2 * buildingSize) + spacing),
                // Bottom-left (cell 0,1) / Top-left (cell 0,0)
                new Coordinate(leftX, bottomY + buildingSize + spacing),
                // Back to start
                new Coordinate(leftX, bottomY)
            };

            return new Polygon(new LinearRing(coordinates.ToArray()));
        }

        private Polygon CreateT4StandardPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // T4 shape: (0,0), (1,0), (2,0), (1,1)
            double leftX = startX + startCol * cellSize + spacing / 2;
            double bottomY = startY + startRow * cellSize + spacing / 2;

            var coordinates = new List<Coordinate>
            {
                // Bottom-left (cell 0,0)
                new Coordinate(leftX, bottomY),
                // Bottom-right (cell 2,0)
                new Coordinate(leftX + (3 * buildingSize) + (2 * spacing), bottomY),
                // Top-right (cell 2,0)
                new Coordinate(leftX + (3 * buildingSize) + (2 * spacing), bottomY + buildingSize),
                // Move left to cell 1,1 top-right
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + buildingSize),
                // Move up to cell 1,1 top-right
                new Coordinate(leftX + (2 * buildingSize) + spacing, bottomY + (2 * buildingSize) + spacing),
                // Move left to cell 1,1 top-left
                new Coordinate(leftX + buildingSize + spacing, bottomY + (2 * buildingSize) + spacing),
                // Move down to cell 1,1 bottom-left
                new Coordinate(leftX + buildingSize + spacing, bottomY + buildingSize),
                // Move left to cell 0,0 top-right
                new Coordinate(leftX, bottomY + buildingSize),
                // Back to start
                new Coordinate(leftX, bottomY)
            };

            return new Polygon(new LinearRing(coordinates.ToArray()));
        }

        #endregion

        #region Fallback Generic Method

        private Polygon CreateGenericPolygon(int startCol, int startRow, double cellSize, double startX, double startY, double buildingSize, double spacing)
        {
            // Original method for any new templates not yet implemented
            var allCorners = new List<Coordinate>();

            foreach (var (colOffset, rowOffset) in Cells)
            {
                int col = startCol + colOffset;
                int row = startRow + rowOffset;

                double cellX = startX + col * cellSize;
                double cellY = startY + row * cellSize;
                double buildingX = cellX + spacing / 2;
                double buildingY = cellY + spacing / 2;

                // Add all 4 corners of this building cell
                allCorners.Add(new Coordinate(buildingX, buildingY));
                allCorners.Add(new Coordinate(buildingX + buildingSize, buildingY));
                allCorners.Add(new Coordinate(buildingX + buildingSize, buildingY + buildingSize));
                allCorners.Add(new Coordinate(buildingX, buildingY + buildingSize));
            }

            // Remove duplicate corners (where cells touch)
            var uniqueCorners = allCorners
                .GroupBy(c => new { X = System.Math.Round(c.X, 3), Y = System.Math.Round(c.Y, 3) })
                .Select(g => g.First())
                .ToList();

            // Sort corners in clockwise order
            if (uniqueCorners.Count >= 3)
            {
                // Calculate centroid
                double centerX = uniqueCorners.Average(c => c.X);
                double centerY = uniqueCorners.Average(c => c.Y);

                // Sort by angle from center
                uniqueCorners.Sort((a, b) =>
                {
                    double angleA = System.Math.Atan2(a.Y - centerY, a.X - centerX);
                    double angleB = System.Math.Atan2(b.Y - centerY, b.X - centerX);
                    return angleA.CompareTo(angleB);
                });

                // Close the polygon
                uniqueCorners.Add(new Coordinate(uniqueCorners[0].X, uniqueCorners[0].Y));

                return new Polygon(new LinearRing(uniqueCorners.ToArray()));
            }

            return null;
        }

        #endregion

        // Get bounding box dimensions
        public (int width, int height) GetDimensions()
        {
            int minCol = Cells.Min(c => c.col);
            int maxCol = Cells.Max(c => c.col);
            int minRow = Cells.Min(c => c.row);
            int maxRow = Cells.Max(c => c.row);

            return (maxCol - minCol + 1, maxRow - minRow + 1);
        }

        // Check if this template can be placed at the given position
        public bool CanPlace(int startCol, int startRow, Cell[,] cellGrid)
        {
            int cols = cellGrid.GetLength(0);
            int rows = cellGrid.GetLength(1);

            foreach (var (colOffset, rowOffset) in Cells)
            {
                int col = startCol + colOffset;
                int row = startRow + rowOffset;

                // Check bounds
                if (col < 0 || col >= cols || row < 0 || row >= rows)
                    return false;

                // Check if cell is available and not assigned
                if (!cellGrid[col, row].IsAvailable || cellGrid[col, row].IsAssigned)
                    return false;
            }

            return true;
        }

        // Place the template (mark cells as assigned)
        public void Place(int startCol, int startRow, Cell[,] cellGrid, int groupId)
        {
            foreach (var (colOffset, rowOffset) in Cells)
            {
                int col = startCol + colOffset;
                int row = startRow + rowOffset;

                cellGrid[col, row].IsAssigned = true;
                cellGrid[col, row].GroupId = groupId;
            }
        }
    }
}