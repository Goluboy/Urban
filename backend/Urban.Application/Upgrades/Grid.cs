using NetTopologySuite.Geometries;
using System.Collections;

namespace Urban.Application.Upgrades
{
    public class Grid
    {
        private readonly double _startX;
        private readonly double _startY;
        private readonly double _endX;
        private readonly double _endY;
        private readonly double _cellSize;
        private readonly BitArray _cells;
        private readonly int _xCells;
        private readonly int _yCells;
        private readonly GeometryFactory _geometryFactory;

        public Grid(Polygon bounds, double cellSize = 20)
        {
            var envelope = bounds.EnvelopeInternal;
            _startX = envelope.MinX;
            _startY = envelope.MinY;
            _endX = envelope.MaxX;
            _endY = envelope.MaxY;
            _cellSize = cellSize;

            var width = _endX - _startX;
            var height = _endY - _startY;

            _xCells = (int)Math.Ceiling(width / _cellSize);
            _yCells = (int)Math.Ceiling(height / _cellSize);

            // Start with all cells as restricted (false)
            _cells = new BitArray(_xCells * _yCells, false);
            _geometryFactory = new GeometryFactory();
        }

        // Public properties
        public int XCells => _xCells;
        public int YCells => _yCells;
        public double CellSize => _cellSize;
        public double StartX => _startX;
        public double StartY => _startY;
        public double EndX => _endX;
        public double EndY => _endY;

        // ===== BASIC OPERATIONS =====

        /// <summary>
        /// Step 1: Mark cells inside a polygon as available (true)
        /// </summary>
        public void MarkAvailableArea(Polygon area)
        {
            for (int col = 0; col < _xCells; col++)
            {
                for (int row = 0; row < _yCells; row++)
                {
                    var cellPolygon = CreateCellPolygon(col, row);

                    if (area.Contains(cellPolygon) || area.Intersects(cellPolygon))
                    {
                        var intersection = area.Intersection(cellPolygon);
                        if (intersection.Area > (_cellSize * _cellSize * 0.5))
                        {
                            _cells[GetCellIndex(col, row)] = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Step 2: Subtract restrictions - mark intersecting cells as restricted (false)
        /// </summary>
        public void SubtractRestrictions(IEnumerable<Polygon> restrictions)
        {
            foreach (var restriction in restrictions)
            {
                var env = restriction.EnvelopeInternal;

                int minCol = Math.Max(0, WorldToGridX(env.MinX));
                int maxCol = Math.Min(_xCells - 1, WorldToGridX(env.MaxX));
                int minRow = Math.Max(0, WorldToGridY(env.MinY));
                int maxRow = Math.Min(_yCells - 1, WorldToGridY(env.MaxY));

                for (int col = minCol; col <= maxCol; col++)
                {
                    for (int row = minRow; row <= maxRow; row++)
                    {
                        var cellPolygon = CreateCellPolygon(col, row);
                        if (restriction.Intersects(cellPolygon))
                        {
                            _cells[GetCellIndex(col, row)] = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the current state of a specific cell
        /// </summary>
        public bool IsCellAvailable(int col, int row)
        {
            if (col < 0 || col >= _xCells || row < 0 || row >= _yCells)
                return false;
            return _cells[GetCellIndex(col, row)];
        }

        // ===== CLUSTERING OPERATIONS =====

        /// <summary>
        /// Cluster 1: Get connected components using 4-directional connectivity
        /// Returns list of clusters, each cluster is list of (col, row) cell coordinates
        /// </summary>
        public List<List<(int col, int row)>> Clusterize4Directional()
        {
            var visited = new bool[_xCells, _yCells];
            var clusters = new List<List<(int col, int row)>>();

            for (int col = 0; col < _xCells; col++)
            {
                for (int row = 0; row < _yCells; row++)
                {
                    if (_cells[GetCellIndex(col, row)] && !visited[col, row])
                    {
                        var cluster = FloodFill4Directional(col, row, visited);
                        if (cluster.Count > 0)
                        {
                            clusters.Add(cluster);
                        }
                    }
                }
            }

            return clusters;
        }

        /// <summary>
        /// Convert a cluster of cells to a polygon (convex hull of cells)
        /// </summary>
        public Polygon ClusterToPolygon(List<(int col, int row)> cluster)
        {
            if (cluster.Count == 0) return null;

            // Get all cell corner points
            var points = new List<Coordinate>();
            foreach (var (col, row) in cluster)
            {
                double x1 = GridToWorldX(col);
                double y1 = GridToWorldY(row);
                double x2 = x1 + _cellSize;
                double y2 = y1 + _cellSize;

                points.Add(new Coordinate(x1, y1));
                points.Add(new Coordinate(x2, y1));
                points.Add(new Coordinate(x2, y2));
                points.Add(new Coordinate(x1, y2));
            }

            // Create convex hull (simplified - using bounding box)
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            var coordinates = new Coordinate[]
            {
                new Coordinate(minX, minY),
                new Coordinate(maxX, minY),
                new Coordinate(maxX, maxY),
                new Coordinate(minX, maxY),
                new Coordinate(minX, minY)
            };

            return new Polygon(new LinearRing(coordinates));
        }

        /// <summary>
        /// Convert a cluster of cells to a precise polygon (follows cell boundaries)
        /// </summary>
        public Polygon ClusterToPrecisePolygon(List<(int col, int row)> cluster)
        {
            if (cluster.Count == 0) return null;

            var cellSet = new HashSet<(int col, int row)>(cluster);
            var edges = new HashSet<Edge>();

            foreach (var (col, row) in cluster)
            {
                double x1 = GridToWorldX(col);
                double y1 = GridToWorldY(row);
                double x2 = x1 + _cellSize;
                double y2 = y1 + _cellSize;

                // Check each edge
                if (!cellSet.Contains((col, row - 1))) // Top
                    edges.Add(new Edge(new Coordinate(x1, y1), new Coordinate(x2, y1)));
                if (!cellSet.Contains((col + 1, row))) // Right
                    edges.Add(new Edge(new Coordinate(x2, y1), new Coordinate(x2, y2)));
                if (!cellSet.Contains((col, row + 1))) // Bottom
                    edges.Add(new Edge(new Coordinate(x2, y2), new Coordinate(x1, y2)));
                if (!cellSet.Contains((col - 1, row))) // Left
                    edges.Add(new Edge(new Coordinate(x1, y2), new Coordinate(x1, y1)));
            }

            return CreatePolygonFromEdges(edges);
        }

        // ===== VISUALIZATION METHODS =====

        /// <summary>
        /// Get all available cells as individual polygons
        /// </summary>
        public List<Polygon> GetAvailableCells()
        {
            var cells = new List<Polygon>();
            for (int col = 0; col < _xCells; col++)
            {
                for (int row = 0; row < _yCells; row++)
                {
                    if (_cells[GetCellIndex(col, row)])
                    {
                        cells.Add(CreateCellPolygon(col, row));
                    }
                }
            }
            return cells;
        }

        /// <summary>
        /// Get all restricted cells as individual polygons
        /// </summary>
        public List<Polygon> GetRestrictedCells()
        {
            var cells = new List<Polygon>();
            for (int col = 0; col < _xCells; col++)
            {
                for (int row = 0; row < _yCells; row++)
                {
                    if (!_cells[GetCellIndex(col, row)])
                    {
                        cells.Add(CreateCellPolygon(col, row));
                    }
                }
            }
            return cells;
        }

        /// <summary>
        /// Get colored visualization of clusters
        /// Returns list of (polygon, clusterId) for each cell
        /// </summary>
        public List<(Polygon polygon, int clusterId)> GetColoredClusters4Directional()
        {
            var clusters = Clusterize4Directional();
            var result = new List<(Polygon polygon, int clusterId)>();

            for (int clusterId = 0; clusterId < clusters.Count; clusterId++)
            {
                foreach (var (col, row) in clusters[clusterId])
                {
                    result.Add((CreateCellPolygon(col, row), clusterId + 1));
                }
            }

            return result;
        }

        /// <summary>
        /// Get cluster polygons (united cells)
        /// </summary>
        public List<Polygon> GetClusterPolygons4Directional(int minCells = 1)
        {
            var clusters = Clusterize4Directional();
            var polygons = new List<Polygon>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count >= minCells)
                {
                    var polygon = ClusterToPrecisePolygon(cluster);
                    if (polygon != null)
                    {
                        polygons.Add(polygon);
                    }
                }
            }

            return polygons;
        }

        // ===== UTILITY METHODS =====

        public bool CanPlacePolygon(Polygon polygon, double buffer = 0)
        {
            var checkPolygon = polygon;
            if (buffer > 0)
            {
                var buffered = polygon.Buffer(buffer);
                if (buffered is Polygon bufferedPolygon)
                {
                    checkPolygon = bufferedPolygon;
                }
            }

            var env = checkPolygon.EnvelopeInternal;
            int minCol = Math.Max(0, WorldToGridX(env.MinX));
            int maxCol = Math.Min(_xCells - 1, WorldToGridX(env.MaxX));
            int minRow = Math.Max(0, WorldToGridY(env.MinY));
            int maxRow = Math.Min(_yCells - 1, WorldToGridY(env.MaxY));

            for (int col = minCol; col <= maxCol; col++)
            {
                for (int row = minRow; row <= maxRow; row++)
                {
                    if (!_cells[GetCellIndex(col, row)])
                    {
                        var cellPolygon = CreateCellPolygon(col, row);
                        if (checkPolygon.Intersects(cellPolygon))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public int GetAvailableCellCount()
        {
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i]) count++;
            }
            return count;
        }

        public int GetRestrictedCellCount()
        {
            return _cells.Length - GetAvailableCellCount();
        }

        // ===== PRIVATE HELPER METHODS =====

        private List<(int col, int row)> FloodFill4Directional(int startCol, int startRow, bool[,] visited)
        {
            var cells = new List<(int col, int row)>();
            var stack = new Stack<(int col, int row)>();
            stack.Push((startCol, startRow));

            while (stack.Count > 0)
            {
                var (col, row) = stack.Pop();
                if (col < 0 || col >= _xCells || row < 0 || row >= _yCells ||
                    visited[col, row] || !_cells[GetCellIndex(col, row)])
                    continue;

                visited[col, row] = true;
                cells.Add((col, row));

                // 4-directional
                stack.Push((col + 1, row));
                stack.Push((col - 1, row));
                stack.Push((col, row + 1));
                stack.Push((col, row - 1));
            }

            return cells;
        }
        private Polygon CreatePolygonFromEdges(HashSet<Edge> edges)
        {
            if (edges.Count == 0) return null;

            var edgeList = new List<Edge>(edges);
            var coordinates = new List<Coordinate>();

            var currentEdge = edgeList[0];
            coordinates.Add(currentEdge.Start);
            coordinates.Add(currentEdge.End);
            edges.Remove(currentEdge);

            while (edges.Count > 0 && coordinates[0].Distance(coordinates[^1]) > 0.001)
            {
                bool found = false;
                foreach (var edge in edges.ToList())
                {
                    if (edge.Start.Distance(coordinates[^1]) < 0.001)
                    {
                        coordinates.Add(edge.End);
                        edges.Remove(edge);
                        found = true;
                        break;
                    }
                    else if (edge.End.Distance(coordinates[^1]) < 0.001)
                    {
                        coordinates.Add(edge.Start);
                        edges.Remove(edge);
                        found = true;
                        break;
                    }
                }

                if (!found) break;
            }

            if (coordinates.Count > 2 && coordinates[0].Distance(coordinates[^1]) > 0.001)
            {
                coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
            }

            return coordinates.Count >= 4 ? new Polygon(new LinearRing(coordinates.ToArray())) : null;
        }

        private int GetCellIndex(int col, int row) => row * _xCells + col;
        private int WorldToGridX(double worldX) => (int)Math.Floor((worldX - _startX) / _cellSize);
        private int WorldToGridY(double worldY) => (int)Math.Floor((worldY - _startY) / _cellSize);
        private double GridToWorldX(int gridX) => _startX + gridX * _cellSize;
        private double GridToWorldY(int gridY) => _startY + gridY * _cellSize;

        private Polygon CreateCellPolygon(int col, int row)
        {
            double x1 = GridToWorldX(col);
            double y1 = GridToWorldY(row);
            double x2 = x1 + _cellSize;
            double y2 = y1 + _cellSize;

            var coordinates = new Coordinate[]
            {
                new Coordinate(x1, y1),
                new Coordinate(x2, y1),
                new Coordinate(x2, y2),
                new Coordinate(x1, y2),
                new Coordinate(x1, y1)
            };

            return new Polygon(new LinearRing(coordinates));
        }

        #region ClusterSubdivision
        // Add these methods to the Grid class:

        // ===== CLUSTER SUBDIVISION OPERATIONS =====

        /// <summary>
        /// Subdivide clusters into blocks with optimal size (6400-14400 m²) by working with cells
        /// </summary>
        public List<Polygon> SubdivideClustersByCells(double minArea = 6400, double maxArea = 14400, double maxAspectRatio = 3.0)
        {
            var clusters = Clusterize4Directional();
            var subdividedBlocks = new List<Polygon>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                // Calculate cluster area in square meters
                double clusterArea = cluster.Count * (_cellSize * _cellSize);

                // If cluster is already within desired size range
                if (clusterArea >= minArea && clusterArea <= maxArea)
                {
                    var clusterPoly = ClusterToPrecisePolygon(cluster);
                    if (clusterPoly != null)
                    {
                        subdividedBlocks.Add(clusterPoly);
                    }
                    continue;
                }

                // Need to subdivide this cluster
                var subClusters = SubdivideClusterByCells(cluster, minArea, maxArea, maxAspectRatio);
                foreach (var subCluster in subClusters)
                {
                    var subPoly = ClusterToPrecisePolygon(subCluster);
                    if (subPoly != null)
                    {
                        subdividedBlocks.Add(subPoly);
                    }
                }
            }

            return subdividedBlocks;
        }

        /// <summary>
        /// Subdivide a single cluster of cells into optimal-sized sub-clusters
        /// </summary>
        private List<List<(int col, int row)>> SubdivideClusterByCells(
            List<(int col, int row)> cluster,
            double minArea,
            double maxArea,
            double maxAspectRatio)
        {
            var result = new List<List<(int col, int row)>>();

            // Calculate target cell count based on area
            double cellsPerSquareMeter = 1.0 / (_cellSize * _cellSize);
            int minCells = (int)Math.Ceiling(minArea * cellsPerSquareMeter);
            int maxCells = (int)Math.Floor(maxArea * cellsPerSquareMeter);
            int targetCells = (minCells + maxCells) / 2;

            // If cluster is small enough, return it as is
            if (cluster.Count <= maxCells)
            {
                result.Add(cluster);
                return result;
            }

            // Find bounding box of the cluster
            int minCol = cluster.Min(c => c.col);
            int maxCol = cluster.Max(c => c.col);
            int minRow = cluster.Min(c => c.row);
            int maxRow = cluster.Max(c => c.row);

            int width = maxCol - minCol + 1;
            int height = maxRow - minRow + 1;

            // Determine optimal grid subdivision
            int gridCols = 1;
            int gridRows = 1;

            // Try to find grid dimensions that give good aspect ratio
            double bestScore = double.MaxValue;

            for (int cols = 1; cols <= Math.Min(5, width); cols++)
            {
                for (int rows = 1; rows <= Math.Min(5, height); rows++)
                {
                    int blockWidth = (int)Math.Ceiling(width / (double)cols);
                    int blockHeight = (int)Math.Ceiling(height / (double)rows);

                    // Calculate aspect ratio
                    double aspectRatio = Math.Max(
                        blockWidth / (double)blockHeight,
                        blockHeight / (double)blockWidth);

                    if (aspectRatio > maxAspectRatio)
                        continue;

                    // Calculate how evenly cells would be distributed
                    int totalBlocks = cols * rows;
                    int estimatedCellsPerBlock = cluster.Count / totalBlocks;

                    // Score based on how close to target size and aspect ratio
                    double sizeScore = Math.Abs(estimatedCellsPerBlock - targetCells) / (double)targetCells;
                    double aspectScore = (aspectRatio - 1) / (maxAspectRatio - 1); // Normalize 1-3 to 0-1

                    double totalScore = sizeScore * 0.7 + aspectScore * 0.3;

                    if (totalScore < bestScore)
                    {
                        bestScore = totalScore;
                        gridCols = cols;
                        gridRows = rows;
                    }
                }
            }

            // If no good grid found, use recursive splitting
            if (bestScore == double.MaxValue)
            {
                return SplitClusterRecursive(cluster, minCells, maxCells, maxAspectRatio);
            }

            // Create grid-based subdivision
            return SplitClusterByGrid(cluster, gridCols, gridRows, minCol, maxCol, minRow, maxRow);
        }

        /// <summary>
        /// Split cluster using a grid pattern
        /// </summary>
        private List<List<(int col, int row)>> SplitClusterByGrid(
            List<(int col, int row)> cluster,
            int cols, int rows,
            int minCol, int maxCol, int minRow, int maxRow)
        {
            var result = new List<List<(int col, int row)>>();
            var cellSet = new HashSet<(int col, int row)>(cluster);

            int totalWidth = maxCol - minCol + 1;
            int totalHeight = maxRow - minRow + 1;

            int blockWidth = (int)Math.Ceiling(totalWidth / (double)cols);
            int blockHeight = (int)Math.Ceiling(totalHeight / (double)rows);

            for (int gridCol = 0; gridCol < cols; gridCol++)
            {
                for (int gridRow = 0; gridRow < rows; gridRow++)
                {
                    int blockMinCol = minCol + gridCol * blockWidth;
                    int blockMaxCol = Math.Min(minCol + (gridCol + 1) * blockWidth - 1, maxCol);
                    int blockMinRow = minRow + gridRow * blockHeight;
                    int blockMaxRow = Math.Min(minRow + (gridRow + 1) * blockHeight - 1, maxRow);

                    var blockCells = new List<(int col, int row)>();

                    // Collect cells within this grid block
                    for (int col = blockMinCol; col <= blockMaxCol; col++)
                    {
                        for (int row = blockMinRow; row <= blockMaxRow; row++)
                        {
                            if (cellSet.Contains((col, row)))
                            {
                                blockCells.Add((col, row));
                            }
                        }
                    }

                    if (blockCells.Count > 0)
                    {
                        result.Add(blockCells);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Recursively split cluster until pieces are optimal size
        /// </summary>
        private List<List<(int col, int row)>> SplitClusterRecursive(
            List<(int col, int row)> cluster,
            int minCells, int maxCells,
            double maxAspectRatio)
        {
            var result = new List<List<(int col, int row)>>();

            // If cluster is already optimal size, return it
            if (cluster.Count <= maxCells)
            {
                result.Add(cluster);
                return result;
            }

            // Find best split direction
            int minCol = cluster.Min(c => c.col);
            int maxCol = cluster.Max(c => c.col);
            int minRow = cluster.Min(c => c.row);
            int maxRow = cluster.Max(c => c.row);

            int width = maxCol - minCol + 1;
            int height = maxRow - minRow + 1;

            bool splitHorizontally = width > height;
            var cellSet = new HashSet<(int col, int row)>(cluster);

            if (splitHorizontally)
            {
                // Split vertically (by columns)
                int splitCol = minCol + width / 2;

                var leftCells = new List<(int col, int row)>();
                var rightCells = new List<(int col, int row)>();

                foreach (var cell in cluster)
                {
                    if (cell.col < splitCol)
                        leftCells.Add(cell);
                    else
                        rightCells.Add(cell);
                }

                // Recursively split both halves
                if (leftCells.Count > 0)
                    result.AddRange(SplitClusterRecursive(leftCells, minCells, maxCells, maxAspectRatio));

                if (rightCells.Count > 0)
                    result.AddRange(SplitClusterRecursive(rightCells, minCells, maxCells, maxAspectRatio));
            }
            else
            {
                // Split horizontally (by rows)
                int splitRow = minRow + height / 2;

                var bottomCells = new List<(int col, int row)>();
                var topCells = new List<(int col, int row)>();

                foreach (var cell in cluster)
                {
                    if (cell.row < splitRow)
                        bottomCells.Add(cell);
                    else
                        topCells.Add(cell);
                }

                // Recursively split both halves
                if (bottomCells.Count > 0)
                    result.AddRange(SplitClusterRecursive(bottomCells, minCells, maxCells, maxAspectRatio));

                if (topCells.Count > 0)
                    result.AddRange(SplitClusterRecursive(topCells, minCells, maxCells, maxAspectRatio));
            }

            return result;
        }

        /// <summary>
        /// Alternative: Subdivide by finding natural "cuts" in the cluster
        /// </summary>
        public List<Polygon> SubdivideClustersByNaturalCuts(double minArea = 6400, double maxArea = 14400)
        {
            var clusters = Clusterize4Directional();
            var subdividedBlocks = new List<Polygon>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                // Calculate cluster area
                double clusterArea = cluster.Count * (_cellSize * _cellSize);

                // If already optimal, keep as is
                if (clusterArea >= minArea && clusterArea <= maxArea)
                {
                    var poly = ClusterToPrecisePolygon(cluster);
                    if (poly != null) subdividedBlocks.Add(poly);
                    continue;
                }

                // Try to find natural subdivisions
                var subClusters = FindNaturalSubdivisions(cluster, minArea, maxArea);

                foreach (var subCluster in subClusters)
                {
                    var poly = ClusterToPrecisePolygon(subCluster);
                    if (poly != null) subdividedBlocks.Add(poly);
                }
            }

            return subdividedBlocks;
        }

        /// <summary>
        /// Find natural subdivision points in a cluster
        /// </summary>
        private List<List<(int col, int row)>> FindNaturalSubdivisions(
            List<(int col, int row)> cluster,
            double minArea, double maxArea)
        {
            var result = new List<List<(int col, int row)>>();
            var cellSet = new HashSet<(int col, int row)>(cluster);

            // Find narrow "necks" in the cluster where we can cut
            var potentialCuts = FindNarrowConnections(cluster, cellSet);

            if (potentialCuts.Count == 0)
            {
                // No natural cuts found, use grid subdivision
                return SubdivideClusterByCells(cluster, minArea, maxArea, 3.0);
            }

            // Try cuts to see if they create optimal-sized pieces
            foreach (var cut in potentialCuts)
            {
                // Temporarily remove cut cells
                var tempSet = new HashSet<(int col, int row)>(cellSet);
                foreach (var cell in cut)
                {
                    tempSet.Remove(cell);
                }

                // Find connected components after cut
                var components = FindConnectedComponents(tempSet);

                // Check if components are optimal size
                bool allOptimal = true;
                foreach (var component in components)
                {
                    double area = component.Count * (_cellSize * _cellSize);
                    if (area < minArea || area > maxArea)
                    {
                        allOptimal = false;
                        break;
                    }
                }

                if (allOptimal && components.Count > 1)
                {
                    return components;
                }
            }

            // If no cuts create optimal pieces, use recursive subdivision
            return SplitClusterRecursive(cluster,
                (int)Math.Ceiling(minArea / (_cellSize * _cellSize)),
                (int)Math.Floor(maxArea / (_cellSize * _cellSize)),
                3.0);
        }

        /// <summary>
        /// Find narrow connections in a cluster (cells with few neighbors)
        /// </summary>
        private List<List<(int col, int row)>> FindNarrowConnections(
            List<(int col, int row)> cluster,
            HashSet<(int col, int row)> cellSet)
        {
            var narrowCells = new List<(int col, int row)>();

            foreach (var cell in cluster)
            {
                int neighborCount = 0;

                // Check 4-directional neighbors
                if (cellSet.Contains((cell.col + 1, cell.row))) neighborCount++;
                if (cellSet.Contains((cell.col - 1, cell.row))) neighborCount++;
                if (cellSet.Contains((cell.col, cell.row + 1))) neighborCount++;
                if (cellSet.Contains((cell.col, cell.row - 1))) neighborCount++;

                // Cells with only 1 or 2 neighbors might be in narrow parts
                if (neighborCount <= 2)
                {
                    narrowCells.Add(cell);
                }
            }

            // Group narrow cells that are adjacent
            var groups = new List<List<(int col, int row)>>();
            var visited = new HashSet<(int col, int row)>();

            foreach (var cell in narrowCells)
            {
                if (visited.Contains(cell)) continue;

                var group = new List<(int col, int row)>();
                var stack = new Stack<(int col, int row)>();
                stack.Push(cell);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (!narrowCells.Contains(current) || visited.Contains(current))
                        continue;

                    visited.Add(current);
                    group.Add(current);

                    // Check adjacent narrow cells
                    var neighbors = new (int col, int row)[]
                    {
                (current.col + 1, current.row),
                (current.col - 1, current.row),
                (current.col, current.row + 1),
                (current.col, current.row - 1)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (narrowCells.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }

                if (group.Count > 0)
                {
                    groups.Add(group);
                }
            }

            return groups;
        }

        /// <summary>
        /// Find connected components in a set of cells
        /// </summary>
        private List<List<(int col, int row)>> FindConnectedComponents(HashSet<(int col, int row)> cellSet)
        {
            var components = new List<List<(int col, int row)>>();
            var visited = new HashSet<(int col, int row)>();

            foreach (var cell in cellSet)
            {
                if (visited.Contains(cell)) continue;

                var component = new List<(int col, int row)>();
                var stack = new Stack<(int col, int row)>();
                stack.Push(cell);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (!cellSet.Contains(current) || visited.Contains(current))
                        continue;

                    visited.Add(current);
                    component.Add(current);

                    // Check 4-directional neighbors
                    var neighbors = new (int col, int row)[]
                    {
                (current.col + 1, current.row),
                (current.col - 1, current.row),
                (current.col, current.row + 1),
                (current.col, current.row - 1)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (cellSet.Contains(neighbor) && !visited.Contains(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }

                if (component.Count > 0)
                {
                    components.Add(component);
                }
            }

            return components;
        }
        #endregion


        private class Edge
        {
            public Coordinate Start { get; }
            public Coordinate End { get; }

            public Edge(Coordinate start, Coordinate end)
            {
                Start = start;
                End = end;
            }

            public override bool Equals(object obj)
            {
                if (obj is Edge other)
                {
                    return (Start.Equals(other.Start) && End.Equals(other.End)) ||
                           (Start.Equals(other.End) && End.Equals(other.Start));
                }
                return false;
            }

            public override int GetHashCode() => Start.GetHashCode() ^ End.GetHashCode();
        }



















        #region Oriented Cluster Subdivision - Larger Blocks

        /// <summary>
        /// Subdivide clusters into larger blocks with optimal size, aiming for blocks near the maximum area.
        /// Creates fewer, larger blocks while maintaining good aspect ratios.
        /// </summary>
        public List<Polygon> SubdivideClustersIntoLargerBlocks(
            double minArea = 6400,
            double maxArea = 14400,
            double maxAspectRatio = 2.0,
            double targetAspectRatio = 1.2)
        {
            var clusters = Clusterize4Directional();
            var subdividedBlocks = new List<Polygon>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                // Calculate cluster area
                double clusterArea = cluster.Count * (_cellSize * _cellSize);

                // If cluster is already within desired size range
                if (clusterArea >= minArea && clusterArea <= maxArea)
                {
                    var clusterPoly = ClusterToPrecisePolygon(cluster);
                    if (clusterPoly != null)
                    {
                        subdividedBlocks.Add(clusterPoly);
                    }
                    continue;
                }

                // Find the orientation of this cluster
                double orientationAngle = GetClusterOrientationFromCells(cluster);

                // Subdivide into larger blocks
                var subClusters = SubdivideIntoLargerBlocks(
                    cluster,
                    minArea,
                    maxArea,
                    maxAspectRatio,
                    targetAspectRatio,
                    orientationAngle);

                foreach (var subCluster in subClusters)
                {
                    var subPoly = ClusterToPrecisePolygon(subCluster);
                    if (subPoly != null)
                    {
                        subdividedBlocks.Add(subPoly);
                    }
                }
            }

            return subdividedBlocks;
        }

        /// <summary>
        /// Subdivide cluster into larger blocks, aiming for blocks near maximum area
        /// </summary>
        private List<List<(int col, int row)>> SubdivideIntoLargerBlocks(
            List<(int col, int row)> cluster,
            double minArea,
            double maxArea,
            double maxAspectRatio,
            double targetAspectRatio,
            double orientationAngle)
        {
            var result = new List<List<(int col, int row)>>();

            // If cluster is small enough, return it as is
            double clusterArea = cluster.Count * (_cellSize * _cellSize);
            if (clusterArea <= maxArea)
            {
                result.Add(cluster);
                return result;
            }

            // Get rotated coordinates
            var rotatedCoords = GetRotatedCellCoordinates(cluster, orientationAngle);

            // Find bounds in rotated coordinate system
            double minX = rotatedCoords.Min(c => c.rotatedX);
            double maxX = rotatedCoords.Max(c => c.rotatedX);
            double minY = rotatedCoords.Min(c => c.rotatedY);
            double maxY = rotatedCoords.Max(c => c.rotatedY);

            double width = maxX - minX;
            double height = maxY - minY;

            // Calculate minimum and maximum cells per block
            double cellsPerSquareMeter = 1.0 / (_cellSize * _cellSize);
            int minCells = (int)Math.Ceiling(minArea * cellsPerSquareMeter);
            int maxCells = (int)Math.Floor(maxArea * cellsPerSquareMeter);

            // Target for larger blocks: aim for 80-90% of max area
            int targetCells = (int)(maxCells * 0.85);

            // Calculate how many blocks we need (round down to get larger blocks)
            int numBlocks = Math.Max(2, (int)Math.Floor((double)cluster.Count / targetCells));

            // Adjust to get good aspect ratio
            (int cols, int rows) = FindOptimalGridForLargerBlocks(width, height, numBlocks, targetAspectRatio);

            // Ensure we're not creating too many blocks
            while (cols * rows > numBlocks * 1.5 && cols > 1 && rows > 1)
            {
                cols = Math.Max(1, cols - 1);
                rows = Math.Max(1, rows - 1);
            }

            // Distribute cells into grid cells
            var gridCells = DistributeToGridCells(rotatedCoords, minX, maxX, minY, maxY, cols, rows);

            // Process each grid cell
            foreach (var gridCell in gridCells)
            {
                if (gridCell.Count == 0) continue;

                double cellArea = gridCell.Count * (_cellSize * _cellSize);

                if (cellArea <= maxArea)
                {
                    // Block is within size limit
                    if (cellArea >= minArea * 0.8) // Allow slightly smaller blocks
                    {
                        // Check aspect ratio
                        double aspect = GetBlockAspectRatio(gridCell);
                        if (aspect <= maxAspectRatio)
                        {
                            result.Add(gridCell);
                        }
                        else
                        {
                            // Split block with bad aspect ratio
                            var splitBlocks = SplitBlockForBetterAspect(gridCell, targetAspectRatio, orientationAngle);
                            foreach (var splitBlock in splitBlocks)
                            {
                                if (splitBlock.Count >= minCells)
                                {
                                    result.Add(splitBlock);
                                }
                                else
                                {
                                    // Too small, merge with neighbors later
                                    result.Add(splitBlock);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Block is too small, will be merged
                        result.Add(gridCell);
                    }
                }
                else
                {
                    // Block is too large, recursively subdivide
                    double subOrientation = GetClusterOrientationFromCells(gridCell);
                    var subBlocks = SubdivideIntoLargerBlocks(
                        gridCell, minArea, maxArea, maxAspectRatio, targetAspectRatio, subOrientation);
                    result.AddRange(subBlocks);
                }
            }

            // Merge small blocks to reach minimum size
            result = MergeBlocksToMinimumSize(result, minCells, maxCells);

            // Final check: ensure all blocks have reasonable aspect ratios
            return FinalizeBlockShapes(result, minCells, maxCells, maxAspectRatio, targetAspectRatio);
        }

        /// <summary>
        /// Find optimal grid dimensions for larger blocks
        /// </summary>
        private (int cols, int rows) FindOptimalGridForLargerBlocks(
            double width, double height,
            int targetBlocks, double targetAspectRatio)
        {
            if (width <= 0 || height <= 0)
                return (Math.Max(2, targetBlocks), 1);

            double clusterAspect = width / height;

            // Start with minimal splits to get larger blocks
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(targetBlocks * clusterAspect)));
            int rows = Math.Max(1, (int)Math.Ceiling((double)targetBlocks / cols));

            // Try to reduce splits to get larger blocks
            while (cols * rows > targetBlocks * 1.2 && cols > 1 && rows > 1)
            {
                if (cols > rows)
                    cols--;
                else
                    rows--;
            }

            // Check aspect ratio of resulting blocks
            double blockAspect = (width / cols) / (height / rows);

            // Adjust to improve aspect ratio if needed
            if (blockAspect > targetAspectRatio * 1.5 || blockAspect < 1.0 / (targetAspectRatio * 1.5))
            {
                // Try alternative configurations
                double bestScore = double.MaxValue;
                (int bestCols, int bestRows) = (cols, rows);

                for (int c = Math.Max(1, cols - 2); c <= cols + 2; c++)
                {
                    for (int r = Math.Max(1, rows - 2); r <= rows + 2; r++)
                    {
                        if (c * r < Math.Ceiling(targetBlocks * 0.7) || c * r > Math.Ceiling(targetBlocks * 1.5))
                            continue;

                        double aspect = (width / c) / (height / r);
                        double aspectScore = Math.Abs(aspect - targetAspectRatio) / targetAspectRatio;
                        double countScore = Math.Abs(c * r - targetBlocks) / (double)targetBlocks;

                        double totalScore = aspectScore * 0.6 + countScore * 0.4;

                        if (totalScore < bestScore)
                        {
                            bestScore = totalScore;
                            bestCols = c;
                            bestRows = r;
                        }
                    }
                }

                cols = bestCols;
                rows = bestRows;
            }

            return (cols, rows);
        }

        /// <summary>
        /// Merge small blocks to reach minimum size
        /// </summary>
        private List<List<(int col, int row)>> MergeBlocksToMinimumSize(
            List<List<(int col, int row)>> blocks,
            int minCells, int maxCells)
        {
            if (blocks.Count <= 1) return blocks;

            var result = new List<List<(int col, int row)>>();
            var merged = new bool[blocks.Count];

            // Sort blocks by size (smallest first, so we merge them first)
            var sortedIndices = Enumerable.Range(0, blocks.Count)
                .OrderBy(i => blocks[i].Count)
                .ToList();

            foreach (int i in sortedIndices)
            {
                if (merged[i]) continue;

                if (blocks[i].Count >= minCells)
                {
                    // Block is already large enough
                    result.Add(blocks[i]);
                    merged[i] = true;
                }
                else
                {
                    // Find best neighbor to merge with
                    int bestNeighbor = -1;
                    double bestScore = double.MaxValue;
                    int bestMergedSize = 0;

                    for (int j = 0; j < blocks.Count; j++)
                    {
                        if (i == j || merged[j]) continue;

                        int mergedSize = blocks[i].Count + blocks[j].Count;

                        // Don't merge if result would be too large
                        if (mergedSize > maxCells * 1.2) continue;

                        // Calculate proximity
                        var centroidI = GetCentroid(blocks[i]);
                        var centroidJ = GetCentroid(blocks[j]);
                        double dx = centroidJ.col - centroidI.col;
                        double dy = centroidJ.row - centroidI.row;
                        double distance = Math.Sqrt(dx * dx + dy * dy);

                        // Prefer merges that create blocks closer to target size
                        double sizeScore = Math.Abs(mergedSize - maxCells * 0.8) / (double)maxCells;

                        double score = distance * 0.5 + sizeScore * 0.5;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestNeighbor = j;
                            bestMergedSize = mergedSize;
                        }
                    }

                    if (bestNeighbor >= 0 && bestMergedSize <= maxCells * 1.2)
                    {
                        // Merge the blocks
                        var mergedBlock = new List<(int col, int row)>(blocks[i]);
                        mergedBlock.AddRange(blocks[bestNeighbor]);
                        result.Add(mergedBlock);
                        merged[i] = true;
                        merged[bestNeighbor] = true;
                    }
                    else
                    {
                        // Can't find a good merge, keep as is
                        result.Add(blocks[i]);
                        merged[i] = true;
                    }
                }
            }

            // Add any unmerged blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!merged[i])
                    result.Add(blocks[i]);
            }

            return result;
        }

        /// <summary>
        /// Split a block to improve aspect ratio
        /// </summary>
        private List<List<(int col, int row)>> SplitBlockForBetterAspect(
            List<(int col, int row)> block,
            double targetAspectRatio,
            double orientation)
        {
            var result = new List<List<(int col, int row)>>();

            // Get rotated coordinates
            var rotatedCoords = GetRotatedCellCoordinates(block, orientation);

            // Find bounds
            double minX = rotatedCoords.Min(c => c.rotatedX);
            double maxX = rotatedCoords.Max(c => c.rotatedX);
            double minY = rotatedCoords.Min(c => c.rotatedY);
            double maxY = rotatedCoords.Max(c => c.rotatedY);

            double width = maxX - minX;
            double height = maxY - minY;

            double currentAspect = width / height;

            if (currentAspect > targetAspectRatio)
            {
                // Block is too wide, split vertically
                int splits = Math.Max(2, (int)Math.Ceiling(currentAspect / targetAspectRatio));

                for (int i = 0; i < splits; i++)
                {
                    double splitStart = minX + i * width / splits;
                    double splitEnd = splitStart + width / splits;

                    var splitCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedX >= splitStart && coord.rotatedX <= splitEnd)
                        {
                            splitCells.Add((coord.col, coord.row));
                        }
                    }

                    if (splitCells.Count > 0)
                        result.Add(splitCells);
                }
            }
            else
            {
                // Block is too tall, split horizontally
                int splits = Math.Max(2, (int)Math.Ceiling(1.0 / currentAspect / targetAspectRatio));

                for (int i = 0; i < splits; i++)
                {
                    double splitStart = minY + i * height / splits;
                    double splitEnd = splitStart + height / splits;

                    var splitCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedY >= splitStart && coord.rotatedY <= splitEnd)
                        {
                            splitCells.Add((coord.col, coord.row));
                        }
                    }

                    if (splitCells.Count > 0)
                        result.Add(splitCells);
                }
            }

            return result;
        }

        /// <summary>
        /// Final check and adjustment of block shapes
        /// </summary>
        private List<List<(int col, int row)>> FinalizeBlockShapes(
            List<List<(int col, int row)>> blocks,
            int minCells, int maxCells,
            double maxAspectRatio, double targetAspectRatio)
        {
            var result = new List<List<(int col, int row)>>();

            foreach (var block in blocks)
            {
                if (block.Count < minCells * 0.7)
                {
                    // Very small block, try to merge with neighbors
                    // For now, just keep it (it will be handled in a post-processing step)
                    result.Add(block);
                    continue;
                }

                double aspect = GetBlockAspectRatio(block);

                if (aspect <= maxAspectRatio)
                {
                    result.Add(block);
                }
                else
                {
                    // Block has bad aspect ratio
                    if (block.Count > maxCells)
                    {
                        // Block is too large and has bad aspect ratio
                        // Find orientation and split
                        double orientation = GetClusterOrientationFromCells(block);
                        var betterBlocks = SplitBlockForBetterAspect(block, targetAspectRatio, orientation);

                        // Check if resulting blocks are good
                        bool allGood = true;
                        foreach (var betterBlock in betterBlocks)
                        {
                            double betterAspect = GetBlockAspectRatio(betterBlock);
                            if (betterAspect > maxAspectRatio * 1.2)
                            {
                                allGood = false;
                                break;
                            }
                        }

                        if (allGood)
                        {
                            result.AddRange(betterBlocks);
                        }
                        else
                        {
                            // Keep original, despite bad aspect ratio
                            result.Add(block);
                        }
                    }
                    else
                    {
                        // Block is within size limits but has bad aspect ratio
                        // Try one split to improve it
                        double orientation = GetClusterOrientationFromCells(block);
                        var betterBlocks = SplitBlockForBetterAspect(block, targetAspectRatio, orientation);

                        // Only keep if split creates reasonable blocks
                        if (betterBlocks.Count == 2)
                        {
                            bool bothGood = true;
                            foreach (var betterBlock in betterBlocks)
                            {
                                if (betterBlock.Count < minCells * 0.5)
                                {
                                    bothGood = false;
                                    break;
                                }
                            }

                            if (bothGood)
                            {
                                result.AddRange(betterBlocks);
                            }
                            else
                            {
                                result.Add(block);
                            }
                        }
                        else
                        {
                            result.Add(block);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Simplified method: Create larger blocks by using fewer splits
        /// </summary>
        public List<Polygon> SubdivideClustersIntoMinimalBlocks(
            double minArea = 6400,
            double maxArea = 14400,
            double maxAspectRatio = 2.0)
        {
            var clusters = Clusterize4Directional();
            var subdividedBlocks = new List<Polygon>();

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                // Calculate cluster area
                double clusterArea = cluster.Count * (_cellSize * _cellSize);

                // If cluster is already within desired size range
                if (clusterArea >= minArea && clusterArea <= maxArea)
                {
                    var clusterPoly = ClusterToPrecisePolygon(cluster);
                    if (clusterPoly != null)
                    {
                        subdividedBlocks.Add(clusterPoly);
                    }
                    continue;
                }

                // Find the orientation of this cluster
                double orientationAngle = GetClusterOrientationFromCells(cluster);

                // Calculate how many blocks we need (minimal number)
                double targetArea = maxArea * 0.9; // Aim for 90% of max area
                int targetBlocks = Math.Max(2, (int)Math.Ceiling(clusterArea / targetArea));

                // Get rotated coordinates
                var rotatedCoords = GetRotatedCellCoordinates(cluster, orientationAngle);

                // Find bounds
                double minX = rotatedCoords.Min(c => c.rotatedX);
                double maxX = rotatedCoords.Max(c => c.rotatedX);
                double minY = rotatedCoords.Min(c => c.rotatedY);
                double maxY = rotatedCoords.Max(c => c.rotatedY);

                double width = maxX - minX;
                double height = maxY - minY;

                // Simple split: divide along longer dimension
                bool splitVertically = width > height;

                var subClusters = new List<List<(int col, int row)>>();

                if (splitVertically)
                {
                    // Split vertically
                    double splitX = minX + width / 2;

                    var leftCells = new List<(int col, int row)>();
                    var rightCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedX < splitX)
                            leftCells.Add((coord.col, coord.row));
                        else
                            rightCells.Add((coord.col, coord.row));
                    }

                    if (leftCells.Count > 0) subClusters.Add(leftCells);
                    if (rightCells.Count > 0) subClusters.Add(rightCells);
                }
                else
                {
                    // Split horizontally
                    double splitY = minY + height / 2;

                    var bottomCells = new List<(int col, int row)>();
                    var topCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedY < splitY)
                            bottomCells.Add((coord.col, coord.row));
                        else
                            topCells.Add((coord.col, coord.row));
                    }

                    if (bottomCells.Count > 0) subClusters.Add(bottomCells);
                    if (topCells.Count > 0) subClusters.Add(topCells);
                }

                // Check if any blocks need further subdivision
                var finalSubClusters = new List<List<(int col, int row)>>();
                foreach (var subCluster in subClusters)
                {
                    double subArea = subCluster.Count * (_cellSize * _cellSize);

                    if (subArea <= maxArea && subArea >= minArea * 0.7)
                    {
                        finalSubClusters.Add(subCluster);
                    }
                    else if (subArea > maxArea)
                    {
                        // Recursively subdivide
                        double subOrientation = GetClusterOrientationFromCells(subCluster);
                        var deeperClusters = SubdivideIntoLargerBlocks(
                            subCluster, minArea, maxArea, maxAspectRatio, 1.2, subOrientation);
                        finalSubClusters.AddRange(deeperClusters);
                    }
                    else
                    {
                        // Too small, keep as is (will be handled in post-processing)
                        finalSubClusters.Add(subCluster);
                    }
                }

                // Convert to polygons
                foreach (var subCluster in finalSubClusters)
                {
                    var subPoly = ClusterToPrecisePolygon(subCluster);
                    if (subPoly != null)
                    {
                        subdividedBlocks.Add(subPoly);
                    }
                }
            }

            return subdividedBlocks;
        }
        // ===== HELPER METHODS FOR ORIENTED SUBDIVISION =====

        /// <summary>
        /// Calculate the orientation of a cluster using Principal Component Analysis (PCA) on cell coordinates
        /// Returns angle in radians (0 = east, positive counter-clockwise)
        /// </summary>
        private double GetClusterOrientationFromCells(List<(int col, int row)> cluster)
        {
            if (cluster.Count < 3)
                return 0.0; // Not enough points to determine orientation

            // Convert to double arrays for calculations
            int n = cluster.Count;
            double[] xs = new double[n];
            double[] ys = new double[n];

            for (int i = 0; i < n; i++)
            {
                xs[i] = cluster[i].col;
                ys[i] = cluster[i].row;
            }

            // Calculate means (centroid)
            double meanX = xs.Average();
            double meanY = ys.Average();

            // Calculate covariance matrix elements
            double covXX = 0, covXY = 0, covYY = 0;

            for (int i = 0; i < n; i++)
            {
                double dx = xs[i] - meanX;
                double dy = ys[i] - meanY;
                covXX += dx * dx;
                covXY += dx * dy;
                covYY += dy * dy;
            }

            covXX /= n;
            covXY /= n;
            covYY /= n;

            // Calculate orientation angle using PCA formula:
            // θ = 0.5 * arctan2(2 * covXY, covXX - covYY)
            double angle = 0.5 * Math.Atan2(2 * covXY, covXX - covYY);

            // Normalize to 0-π range (0 to 180 degrees)
            if (angle < 0)
                angle += Math.PI;

            // For rectangles, orientation is modulo 90 degrees (π/2)
            // Take the angle modulo 90 degrees and normalize to 0-45 degrees
            angle = angle % (Math.PI / 2);
            if (angle > Math.PI / 4)
                angle -= Math.PI / 2;

            // Snap to 0 if very close (within 5 degrees)
            if (Math.Abs(angle) < Math.PI / 36) // 5 degrees
                angle = 0;

            return angle;
        }

        /// <summary>
        /// Get rotated coordinates for all cells in a cluster
        /// </summary>
        private List<(int col, int row, double rotatedX, double rotatedY)> GetRotatedCellCoordinates(
            List<(int col, int row)> cluster, double angle)
        {
            var result = new List<(int col, int row, double rotatedX, double rotatedY)>();

            // Calculate centroid
            double meanX = cluster.Average(c => c.col);
            double meanY = cluster.Average(c => c.row);

            // Pre-compute trig values
            double cosAngle = Math.Cos(-angle); // Negative angle to rotate coordinate system
            double sinAngle = Math.Sin(-angle);

            foreach (var (col, row) in cluster)
            {
                // Center coordinates around centroid
                double dx = col - meanX;
                double dy = row - meanY;

                // Apply rotation
                double rotatedX = dx * cosAngle - dy * sinAngle;
                double rotatedY = dx * sinAngle + dy * cosAngle;

                result.Add((col, row, rotatedX, rotatedY));
            }

            return result;
        }

        /// <summary>
        /// Calculate centroid of a block
        /// </summary>
        private (double col, double row) GetCentroid(List<(int col, int row)> block)
        {
            if (block.Count == 0) return (0, 0);

            double sumCol = 0, sumRow = 0;
            foreach (var cell in block)
            {
                sumCol += cell.col;
                sumRow += cell.row;
            }

            return (sumCol / block.Count, sumRow / block.Count);
        }

        /// <summary>
        /// Calculate aspect ratio of a block (width/height)
        /// </summary>
        private double GetBlockAspectRatio(List<(int col, int row)> block)
        {
            if (block.Count < 2) return 1.0;

            int minCol = block.Min(c => c.col);
            int maxCol = block.Max(c => c.col);
            int minRow = block.Min(c => c.row);
            int maxRow = block.Max(c => c.row);

            double width = (maxCol - minCol + 1) * _cellSize;
            double height = (maxRow - minRow + 1) * _cellSize;

            if (height < 0.1) return double.MaxValue;
            if (width < 0.1) return 0.0;

            return Math.Max(width / height, height / width);
        }

        /// <summary>
        /// Distribute cells into a grid in rotated coordinate space
        /// </summary>
        private List<List<(int col, int row)>> DistributeToGridCells(
            List<(int col, int row, double rotatedX, double rotatedY)> rotatedCoords,
            double minX, double maxX, double minY, double maxY,
            int gridCols, int gridRows)
        {
            var gridCells = new List<List<(int col, int row)>>(gridCols * gridRows);
            for (int i = 0; i < gridCols * gridRows; i++)
            {
                gridCells.Add(new List<(int col, int row)>());
            }

            double cellWidth = (maxX - minX) / gridCols;
            double cellHeight = (maxY - minY) / gridRows;

            // Avoid division by zero
            if (cellWidth <= 0) cellWidth = 1;
            if (cellHeight <= 0) cellHeight = 1;

            foreach (var coord in rotatedCoords)
            {
                // Calculate grid indices
                int colIndex = Math.Min(gridCols - 1, (int)((coord.rotatedX - minX) / cellWidth));
                int rowIndex = Math.Min(gridRows - 1, (int)((coord.rotatedY - minY) / cellHeight));

                int cellIndex = rowIndex * gridCols + colIndex;
                gridCells[cellIndex].Add((coord.col, coord.row));
            }

            return gridCells;
        }

        /// <summary>
        /// Simplified version for minimal splitting
        /// </summary>
        private List<List<(int col, int row)>> SplitClusterIntoTwo(
            List<(int col, int row)> cluster,
            double orientationAngle)
        {
            var result = new List<List<(int col, int row)>>();

            // Get rotated coordinates
            var rotatedCoords = GetRotatedCellCoordinates(cluster, orientationAngle);

            // Find bounds
            double minX = rotatedCoords.Min(c => c.rotatedX);
            double maxX = rotatedCoords.Max(c => c.rotatedX);
            double minY = rotatedCoords.Min(c => c.rotatedY);
            double maxY = rotatedCoords.Max(c => c.rotatedY);

            double width = maxX - minX;
            double height = maxY - minY;

            // Decide whether to split along width or height
            bool splitVertically = width > height;

            if (splitVertically)
            {
                // Split vertically (reduce width)
                double splitX = minX + width / 2;

                var leftCells = new List<(int col, int row)>();
                var rightCells = new List<(int col, int row)>();

                foreach (var coord in rotatedCoords)
                {
                    if (coord.rotatedX < splitX)
                        leftCells.Add((coord.col, coord.row));
                    else
                        rightCells.Add((coord.col, coord.row));
                }

                if (leftCells.Count > 0) result.Add(leftCells);
                if (rightCells.Count > 0) result.Add(rightCells);
            }
            else
            {
                // Split horizontally (reduce height)
                double splitY = minY + height / 2;

                var bottomCells = new List<(int col, int row)>();
                var topCells = new List<(int col, int row)>();

                foreach (var coord in rotatedCoords)
                {
                    if (coord.rotatedY < splitY)
                        bottomCells.Add((coord.col, coord.row));
                    else
                        topCells.Add((coord.col, coord.row));
                }

                if (bottomCells.Count > 0) result.Add(bottomCells);
                if (topCells.Count > 0) result.Add(topCells);
            }

            return result;
        }

        /// <summary>
        /// Alternative: Create oriented bounding boxes and split along major axis
        /// </summary>
        private List<List<(int col, int row)>> CreateOrientedBlocks(
            List<(int col, int row)> cluster,
            double orientationAngle,
            int targetBlocks)
        {
            var result = new List<List<(int col, int row)>>();

            // Get rotated coordinates
            var rotatedCoords = GetRotatedCellCoordinates(cluster, orientationAngle);

            // Find bounds in rotated coordinate system
            double minX = rotatedCoords.Min(c => c.rotatedX);
            double maxX = rotatedCoords.Max(c => c.rotatedX);
            double minY = rotatedCoords.Min(c => c.rotatedY);
            double maxY = rotatedCoords.Max(c => c.rotatedY);

            double width = maxX - minX;
            double height = maxY - minY;

            // Determine split direction (along the longer dimension)
            bool splitAlongWidth = width > height;

            int splits;
            if (splitAlongWidth)
            {
                splits = Math.Max(2, (int)Math.Ceiling(width / Math.Sqrt(width * height / targetBlocks)));
            }
            else
            {
                splits = Math.Max(2, (int)Math.Ceiling(height / Math.Sqrt(width * height / targetBlocks)));
            }

            // Limit splits to reasonable number
            splits = Math.Min(splits, 4);

            // Create splits
            if (splitAlongWidth)
            {
                double splitWidth = width / splits;

                for (int i = 0; i < splits; i++)
                {
                    double splitStart = minX + i * splitWidth;
                    double splitEnd = splitStart + splitWidth;

                    var blockCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedX >= splitStart && coord.rotatedX <= splitEnd)
                        {
                            blockCells.Add((coord.col, coord.row));
                        }
                    }

                    if (blockCells.Count > 0)
                        result.Add(blockCells);
                }
            }
            else
            {
                double splitHeight = height / splits;

                for (int i = 0; i < splits; i++)
                {
                    double splitStart = minY + i * splitHeight;
                    double splitEnd = splitStart + splitHeight;

                    var blockCells = new List<(int col, int row)>();

                    foreach (var coord in rotatedCoords)
                    {
                        if (coord.rotatedY >= splitStart && coord.rotatedY <= splitEnd)
                        {
                            blockCells.Add((coord.col, coord.row));
                        }
                    }

                    if (blockCells.Count > 0)
                        result.Add(blockCells);
                }
            }

            return result;
        }

        /// <summary>
        /// Fast method for calculating orientation (alternative to PCA)
        /// </summary>
        private double GetOrientationFromBoundingBox(List<(int col, int row)> cluster)
        {
            if (cluster.Count < 3) return 0.0;

            // Get min and max coordinates
            int minCol = cluster.Min(c => c.col);
            int maxCol = cluster.Max(c => c.col);
            int minRow = cluster.Min(c => c.row);
            int maxRow = cluster.Max(c => c.row);

            double width = maxCol - minCol;
            double height = maxRow - minRow;

            if (width <= 0 || height <= 0) return 0.0;

            // Simple orientation based on aspect ratio
            // For very rectangular clusters, orientation is along the longer side
            if (width > height * 1.5)
            {
                // Cluster is wider than tall, orientation is 0 degrees (horizontal)
                return 0.0;
            }
            else if (height > width * 1.5)
            {
                // Cluster is taller than wide, orientation is 90 degrees (vertical)
                return Math.PI / 2;
            }
            else
            {
                // Roughly square, check if there's a diagonal pattern
                // Count cells in each quadrant relative to center
                double centerCol = (minCol + maxCol) / 2.0;
                double centerRow = (minRow + maxRow) / 2.0;

                int tl = 0, tr = 0, bl = 0, br = 0;

                foreach (var (col, row) in cluster)
                {
                    if (col < centerCol)
                    {
                        if (row < centerRow) tl++;
                        else bl++;
                    }
                    else
                    {
                        if (row < centerRow) tr++;
                        else br++;
                    }
                }

                // Check for diagonal dominance
                if (tl + br > tr + bl * 1.5)
                {
                    // Diagonal from top-left to bottom-right
                    return Math.PI / 4;
                }
                else if (tr + bl > tl + br * 1.5)
                {
                    // Diagonal from top-right to bottom-left
                    return -Math.PI / 4;
                }
            }

            return 0.0;
        }
        #endregion
    }
}