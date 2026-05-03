using static System.Math;
using HuGeo.Core.Ellipsoids;

namespace HuGeo.Core.Math
{
    public static class EllipsoidMath
    {
        public static (double X, double Y, double Z) EllipsoidToGeocentric(
            double latitudeRadians,
            double longitudeRadians,
            double heightMeters,
            EllipsoidParameters ellipsoid)
        {
            var a = ellipsoid.SemiMajorAxis;
            var e2 = ellipsoid.FirstEccentricitySquared;

            var sinLat = Sin(latitudeRadians);
            var cosLat = Cos(latitudeRadians);
            var sinLon = Sin(longitudeRadians);
            var cosLon = Cos(longitudeRadians);

            var rn = a / Sqrt(1.0 - e2 * sinLat * sinLat);

            var x = (rn + heightMeters) * cosLat * cosLon;
            var y = (rn + heightMeters) * cosLat * sinLon;
            var z = (rn * (1.0 - e2) + heightMeters) * sinLat;

            return (x, y, z);
        }

        public static (double Latitude, double Longitude, double Height) GeocentricToEllipsoid(
            double x,
            double y,
            double z,
            EllipsoidParameters ellipsoid)
        {
            var a = ellipsoid.SemiMajorAxis;
            var b = ellipsoid.SemiMinorAxis;
            var e2 = ellipsoid.FirstEccentricitySquared;

            var p = Sqrt(x * x + y * y);
            var rh = Sqrt(p * p + z * z);

            var lat = Atan2(z, p * (1 - e2 * a / rh));

            for (int iter = 0; iter < 10; iter++)
            {
                var sinLat = Sin(lat);
                var n = a / Sqrt(1 - e2 * sinLat * sinLat);
                var latNew = Atan2(z + e2 * n * sinLat, p);

                if (Abs(latNew - lat) < 1e-12)
                {
                    lat = latNew;
                    break;
                }
                lat = latNew;
            }

            var sinLat2 = Sin(lat);
            var n2 = a / Sqrt(1 - e2 * sinLat2 * sinLat2);

            var lon = Atan2(y, x);
            var height = p / Cos(lat) - n2;

            if (Cos(lat) < 0.001)
                height = Abs(z) - b;

            return (lat, lon, height);
        }

        public static double RadiansToDegrees(double radians) => radians * 180.0 / PI;

        public static double DegreesToRadians(double degrees) => degrees * PI / 180.0;

        public static (double Degrees, double Minutes, double Seconds) DecimalDegreesToDms(double decimalDegrees)
        {
            var absDegrees = Abs(decimalDegrees);
            var degrees = Floor(absDegrees);
            var minutesDecimal = (absDegrees - degrees) * 60;
            var minutes = Floor(minutesDecimal);
            var seconds = (minutesDecimal - minutes) * 60;

            return (decimalDegrees < 0 ? -degrees : degrees, minutes, seconds);
        }

        public static double DmsToDecimalDegrees(double degrees, double minutes, double seconds)
        {
            var sign = degrees < 0 ? -1 : 1;
            return sign * (Abs(degrees) + minutes / 60.0 + seconds / 3600.0);
        }
    }
}
