using NetTopologySuite.Geometries;
using Urban.Application.Logging;
using Urban.Domain.Geometry;

namespace Urban.Application.OldServices;

public class InsolationCalculator
{
    public class SolarPosition
    {
        public TimeSpan Time;
        public double Azimuth;
        public double Elevation;
    }

    private readonly SolarPosition[] _solarPositions;
    private readonly int _positiveThreshold;
    
    public InsolationCalculator(double lat, double lon)
    {
        var step = TimeSpan.FromMinutes(30);
        var date = new DateTime(2025, 03, 21);

        _positiveThreshold = (int) Math.Ceiling(2.0 / step.TotalHours); 
        _solarPositions = GetSolarPositions(lat, lon, date, step)
            .Where(p => p.Elevation > 15)
            .ToArray();
    }

    private static List<SolarPosition> GetSolarPositions(double lat, double lon, DateTime date, TimeSpan step)
    {
        const double Rad2Deg = 180.0 / Math.PI;

        double Deg(double deg) => deg * Math.PI / 180.0;
        double GetJulianDay(DateTime dt)
        {
            int Y = dt.Year, M = dt.Month;
            double D = dt.Day + dt.Hour / 24.0 + dt.Minute / 1440.0 + dt.Second / 86400.0;
            if (M <= 2) { Y--; M += 12; }
            int A = Y / 100, B = 2 - A + A / 4;
            return Math.Floor(365.25 * (Y + 4716)) + Math.Floor(30.6001 * (M + 1)) + D + B - 1524.5;
        }   

        var results = new List<SolarPosition>();

        var dateTime = date;

        while (dateTime < date.AddDays(1))
        {
            var jd = GetJulianDay(dateTime);
            var jc = (jd - 2451545) / 36525;

            var L0 = (280.46646 + jc * (36000.76983 + 0.0003032 * jc)) % 360;
            var M = 357.52911 + jc * (35999.05029 - 0.0001537 * jc);
            var e = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);

            var C = Math.Sin(Deg(M)) * (1.914602 - jc * 0.004817 - 0.000014 * jc * jc)
                    + Math.Sin(Deg(2 * M)) * (0.019993 - 0.000101 * jc)
                    + Math.Sin(Deg(3 * M)) * 0.000289;

            var trueLong = L0 + C;
            var omega = 125.04 - 1934.136 * jc;
            var lambda = trueLong - 0.00569 - 0.00478 * Math.Sin(Deg(omega));

            var epsilon0 = 23 + (26 + ((21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60)) / 60;
            var epsilon = epsilon0 + 0.00256 * Math.Cos(Deg(omega));

            var delta = Math.Asin(Math.Sin(Deg(epsilon)) * Math.Sin(Deg(lambda)));

            var y = Math.Tan(Deg(epsilon / 2));
            y *= y;

            var Etime = 4 * Rad2Deg * (y * Math.Sin(2 * Deg(L0))
                                       - 2 * e * Math.Sin(Deg(M))
                                       + 4 * e * y * Math.Sin(Deg(M)) * Math.Cos(2 * Deg(L0))
                                       - 0.5 * y * y * Math.Sin(4 * Deg(L0))
                                       - 1.25 * e * e * Math.Sin(2 * Deg(M)));
            
            var TST = dateTime.TimeOfDay.TotalMinutes + Etime + 4 * lon;
            while (TST < 0) TST += 1440;
            while (TST >= 1440) TST -= 1440;

            var HA = (TST / 4.0) - 180.0;

            var haRad = Deg(HA);
            var latRad = Deg(lat);

            var elev = Math.Asin(Math.Sin(latRad) * Math.Sin(delta) + Math.Cos(latRad) * Math.Cos(delta) * Math.Cos(haRad));
            var az = Math.Acos((Math.Sin(delta) - Math.Sin(latRad) * Math.Sin(elev)) / (Math.Cos(latRad) * Math.Cos(elev)));

            var azimuth = Rad2Deg * az;
            if (HA > 0) azimuth = 360 - azimuth;

            var elevation = Rad2Deg * elev;
            results.Add(new SolarPosition { Time = dateTime.TimeOfDay, Azimuth = azimuth, Elevation = elevation });

            dateTime += step;
        }
        
        return results;
    }

    public void CalculateInsolation(Section[] sections)
    {
        using (TimeLogger.Measure("CalculateInsolation"))
        {
            foreach (var bay in sections.SelectMany(s => s.Bays))
                bay.ShadowHeights = new List<double>();

            foreach (var position in _solarPositions)
            {
                foreach (var section in sections)
                {
                    foreach (var bay in section.Bays)
                    {
                        var mid = new Coordinate(
                            (bay.Start.X + bay.End.X) / 2.0,
                            (bay.Start.Y + bay.End.Y) / 2.0
                        );

                        var dx = bay.End.X - bay.Start.X;
                        var dy = bay.End.Y - bay.Start.Y;
                        var len = Math.Sqrt(dx * dx + dy * dy);
                        var nx = -dy / len;
                        var ny = dx / len;
                        var dot = nx * position.Azimuth + ny * position.Elevation;

                        if (dot <= 0)
                        {
                            bay.ShadowHeights.Add(double.PositiveInfinity);
                            continue;
                        }

                        double shadowHeight = 0.0;
                        var rayEnd = new Coordinate(mid.X - position.Azimuth * 1000, mid.Y - position.Elevation * 1000);

                        foreach (var other in sections)
                        {
                            if (other == section) continue;

                            var coords = other.Polygon.Coordinates;
                            for (int i = 0; i < coords.Length - 1; i++)
                            {
                                var dist = IntersectDistance(mid, rayEnd, coords[i], coords[i + 1]);
                                if (!double.IsInfinity(dist))
                                {
                                    double h = other.Height - dist * Math.Tan(position.Elevation * Math.PI / 180.0);
                                    if (h > shadowHeight)
                                        shadowHeight = h;
                                }
                            }
                        }
                        bay.ShadowHeights.Add(shadowHeight < 0 ? 0 : shadowHeight);
                    }
                }
            }
            foreach (var bay in sections.SelectMany(s => s.Bays))
                bay.ResultShadowHeight = bay.ShadowHeights.OrderBy(h => h).Skip(_positiveThreshold - 1).First();
        }
    }

    private static double IntersectDistance(Coordinate a1, Coordinate a2, Coordinate b1, Coordinate b2)
    {
        var dx1 = a2.X - a1.X;
        var dy1 = a2.Y - a1.Y;
        var dx2 = b2.X - b1.X;
        var dy2 = b2.Y - b1.Y;
        var det = dx1 * dy2 - dy1 * dx2;

        if (Math.Abs(det) < 1e-10)
            return double.PositiveInfinity;

        var deltaX = b1.X - a1.X;
        var deltaY = b1.Y - a1.Y;

        var t = (deltaX * dy2 - deltaY * dx2) / det;
        var u = (deltaX * dy1 - deltaY * dx1) / det;

        if (t < 0 || t > 1 || u < 0 || u > 1)
            return double.PositiveInfinity;

        return Math.Sqrt((dx1 * t) * (dx1 * t) + (dy1 * t) * (dy1 * t));
    }
}