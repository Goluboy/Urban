using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Urban.Application.Helpers
{
    public static class CoordinatesConverter
    {
        private static readonly GeographicCoordinateSystem WGS84;
        private static readonly CoordinateTransformationFactory Ctf;

        static CoordinatesConverter()
        {
            // Initialize WGS84 (EPSG:4326)
            WGS84 = GeographicCoordinateSystem.WGS84;
            Ctf = new CoordinateTransformationFactory();
        }

        /// <summary>
        /// Determines the appropriate UTM zone based on longitude
        /// </summary>
        /// <param name="longitude">Longitude in decimal degrees</param>
        /// <returns>UTM zone number (1-60)</returns>
        public static int GetUtmZone(double longitude)
        {
            // UTM zones are 6 degrees wide, starting from 180°W (zone 1)
            // Formula: zone = ((longitude + 180) / 6) + 1
            int zone = (int)((longitude + 180) / 6) + 1;
            
            // Handle edge cases
            if (zone < 1) zone = 1;
            if (zone > 60) zone = 60;
            
            return zone;
        }

        /// <summary>
        /// Determines if the latitude is in the northern hemisphere
        /// </summary>
        /// <param name="latitude">Latitude in decimal degrees</param>
        /// <returns>True if northern hemisphere, false if southern</returns>
        public static bool IsNorthernHemisphere(double latitude)
        {
            return latitude >= 0;
        }

        /// <summary>
        /// Gets the representative coordinate from a geometry for UTM zone determination
        /// </summary>
        /// <param name="geometry">The geometry to analyze</param>
        /// <returns>A representative coordinate (centroid or first point)</returns>
        private static Coordinate GetRepresentativeCoordinate(Geometry geometry)
        {
            if (geometry is NetTopologySuite.Geometries.Point point)
            {
                return point.Coordinate;
            }
            else if (geometry is LineString lineString && lineString.Coordinates.Length > 0)
            {
                return lineString.Coordinates[0];
            }
            else if (geometry is Polygon polygon && polygon.Coordinates.Length > 0)
            {
                return polygon.Coordinates[0];
            }
            else if (geometry is MultiPolygon multiPolygon && multiPolygon.Geometries.Length > 0)
            {
                var firstPolygon = multiPolygon.Geometries[0] as Polygon;
                return firstPolygon?.Coordinates[0] ?? new Coordinate(0, 0);
            }
            else
            {
                // Fallback to centroid
                return geometry.Centroid.Coordinate;
            }
        }

        /// <summary>
        /// Converts geometry from WGS84 to the appropriate UTM system based on geometry location
        /// </summary>
        /// <param name="geometry">The geometry in WGS84 to convert</param>
        /// <returns>Tuple containing (converted geometry, utm coordinate system)</returns>
        public static (Geometry ConvertedGeometry, ProjectedCoordinateSystem UtmSystem) ToUtm(Geometry geometry)
        {
            var repCoord = GetRepresentativeCoordinate(geometry);
            int utmZone = GetUtmZone(repCoord.X);
            bool isNorthern = IsNorthernHemisphere(repCoord.Y);
            
            // Create UTM coordinate system
            var utmSystem = ProjectedCoordinateSystem.WGS84_UTM(utmZone, isNorthern);
            
            // Create transformation
            var transformation = Ctf.CreateFromCoordinateSystems(WGS84, utmSystem);
            
            // Convert geometry
            var convertedGeometry = ConvertGeometry(geometry, transformation);
            
            return (convertedGeometry, utmSystem);
        }

        /// <summary>
        /// Converts geometry from a specific UTM system back to WGS84
        /// </summary>
        /// <param name="geometry">The geometry in UTM coordinates</param>
        /// <param name="utmZone">UTM zone number (1-60)</param>
        /// <param name="isNorthernHemisphere">True if northern hemisphere, false if southern</param>
        /// <returns>The geometry converted back to WGS84</returns>
        public static Geometry FromUtm(Geometry geometry, int utmZone, bool isNorthernHemisphere)
        {
            // Create UTM coordinate system
            var utmSystem = ProjectedCoordinateSystem.WGS84_UTM(utmZone, isNorthernHemisphere);
            
            // Create transformation from UTM to WGS84
            var transformation = Ctf.CreateFromCoordinateSystems(utmSystem, WGS84);
            
            // Convert geometry
            return ConvertGeometry(geometry, transformation);
        }

        /// <summary>
        /// Converts geometry from a specific UTM system back to WGS84
        /// </summary>
        /// <param name="geometry">The geometry in UTM coordinates</param>
        /// <param name="utmSystem">The UTM coordinate system</param>
        /// <returns>The geometry converted back to WGS84</returns>
        public static Geometry FromUtm(Geometry geometry, ProjectedCoordinateSystem utmSystem)
        {
            // Create transformation from UTM to WGS84
            var transformation = Ctf.CreateFromCoordinateSystems(utmSystem, WGS84);
            
            // Convert geometry
            return ConvertGeometry(geometry, transformation);
        }

        /// <summary>
        /// Converts geometry from a specific UTM system back to WGS84 using EPSG code
        /// </summary>
        /// <param name="geometry">The geometry in UTM coordinates</param>
        /// <param name="utmEpsgCode">UTM EPSG code (e.g., "EPSG:32641" for UTM 41N)</param>
        /// <returns>The geometry converted back to WGS84</returns>
        public static Geometry FromUtm(Geometry geometry, string utmEpsgCode)
        {
            // Parse EPSG code to get zone and hemisphere
            // EPSG:326xx = UTM zone xx N, EPSG:327xx = UTM zone xx S
            if (!utmEpsgCode.StartsWith("EPSG:"))
                throw new ArgumentException("Invalid EPSG code format. Expected format: EPSG:326xx or EPSG:327xx");
            
            int epsgNumber = int.Parse(utmEpsgCode.Substring(5));
            int utmZone = epsgNumber % 100;
            bool isNorthern = epsgNumber < 32700; // 326xx = N, 327xx = S
            
            return FromUtm(geometry, utmZone, isNorthern);
        }

        /// <summary>
        /// Gets UTM zone information from a projected coordinate system
        /// </summary>
        /// <param name="utmSystem">The UTM coordinate system</param>
        /// <returns>Tuple containing (zone number, is northern hemisphere, epsg code)</returns>
        public static (int Zone, bool IsNorthern, string EpsgCode) GetUtmInfo(ProjectedCoordinateSystem utmSystem)
        {
            // Extract zone and hemisphere from the coordinate system name
            var name = utmSystem.Name;
            if (name.Contains("UTM"))
            {
                // Parse "WGS 84 / UTM zone XXN" or "WGS 84 / UTM zone XXS"
                var zoneMatch = System.Text.RegularExpressions.Regex.Match(name, @"zone (\d+)([NS])");
                if (zoneMatch.Success)
                {
                    int zone = int.Parse(zoneMatch.Groups[1].Value);
                    bool isNorthern = zoneMatch.Groups[2].Value == "N";
                    string epsgCode = $"EPSG:{32600 + zone + (isNorthern ? 0 : 100)}";
                    return (zone, isNorthern, epsgCode);
                }
            }
            
            // Fallback: try to extract from authority code
            if (utmSystem.AuthorityCode > 0)
            {
                int epsgNumber = (int)utmSystem.AuthorityCode;
                int zone = epsgNumber % 100;
                bool isNorthern = epsgNumber < 32700; // 326xx = N, 327xx = S
                string epsgCode = $"EPSG:{epsgNumber}";
                return (zone, isNorthern, epsgCode);
            }
            
            throw new ArgumentException("Unable to extract UTM information from coordinate system");
        }

        /// <summary>
        /// Converts a single coordinate using the specified transformation
        /// </summary>
        private static Coordinate ConvertCoordinate(Coordinate coord, ICoordinateTransformation transformation)
        {
            var transformed = transformation.MathTransform.Transform(new[] { coord.X, coord.Y });
            return new Coordinate(transformed[0], transformed[1]);
        }

        /// <summary>
        /// Converts geometry using the specified transformation
        /// </summary>
        private static Geometry ConvertGeometry(Geometry geometry, ICoordinateTransformation transformation)
        {
            if (geometry is Polygon polygon)
            {
                var convertedCoords = polygon.Coordinates.Select(c => ConvertCoordinate(c, transformation)).ToArray();
                var convertedRing = new LinearRing(convertedCoords);
                var convertedHoles = polygon.Holes.Select(h => 
                    new LinearRing(h.Coordinates.Select(c => ConvertCoordinate(c, transformation)).ToArray())).ToArray();
                return new Polygon(convertedRing, convertedHoles);
            }
            else if (geometry is MultiPolygon multiPolygon)
            {
                var convertedPolygons = multiPolygon.Geometries.OfType<Polygon>().Select(p => 
                {
                    var convertedCoords = p.Coordinates.Select(c => ConvertCoordinate(c, transformation)).ToArray();
                    var convertedRing = new LinearRing(convertedCoords);
                    var convertedHoles = p.Holes.Select(h => 
                        new LinearRing(h.Coordinates.Select(c => ConvertCoordinate(c, transformation)).ToArray())).ToArray();
                    return new Polygon(convertedRing, convertedHoles);
                }).ToArray();
                return new MultiPolygon(convertedPolygons);
            }
            else if (geometry is NetTopologySuite.Geometries.Point point)
            {
                var convertedCoord = ConvertCoordinate(point.Coordinate, transformation);
                return new NetTopologySuite.Geometries.Point(convertedCoord);
            }
            else if (geometry is LineString lineString)
            {
                var convertedCoords = lineString.Coordinates.Select(c => ConvertCoordinate(c, transformation)).ToArray();
                return new LineString(convertedCoords);
            }
            
            return geometry;
        }


    }
}