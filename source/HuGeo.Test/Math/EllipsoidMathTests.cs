using HuGeo.Core.Ellipsoids;
using HuGeo.Core.Math;

namespace HuGeo.Tests.Math;

public class EllipsoidMathTests
{
    private const double DegToRad = System.Math.PI / 180.0;

    [Fact]
    public void RadiansToDegrees_KnownValue_ReturnsCorrect()
    {
        var result = EllipsoidMath.RadiansToDegrees(System.Math.PI);
        Assert.Equal(180.0, result, precision: 10);
    }

    [Fact]
    public void DegreesToRadians_KnownValue_ReturnsCorrect()
    {
        var result = EllipsoidMath.DegreesToRadians(180.0);
        Assert.Equal(System.Math.PI, result, precision: 10);
    }

    [Fact]
    public void EllipsoidToGeocentric_WGS84_Budapest_RoundTrip()
    {
        // Budapest approx: 47.5°N, 19.0°E, 200m
        var lat = 47.5 * DegToRad;
        var lon = 19.0 * DegToRad;
        var h = 200.0;

        var (x, y, z) = EllipsoidMath.EllipsoidToGeocentric(lat, lon, h, WGS84.Parameters);
        var (lat2, lon2, h2) = EllipsoidMath.GeocentricToEllipsoid(x, y, z, WGS84.Parameters);

        Assert.Equal(lat, lat2, precision: 10);
        Assert.Equal(lon, lon2, precision: 10);
        Assert.Equal(h, h2, 0.001);
    }

    [Theory]
    [InlineData(47.0, 18.0, 100.0)]
    [InlineData(48.5, 22.0, 500.0)]
    [InlineData(45.8, 16.2, 80.0)]
    public void EllipsoidToGeocentric_GRS67_RoundTrip(double latDeg, double lonDeg, double height)
    {
        var lat = latDeg * DegToRad;
        var lon = lonDeg * DegToRad;

        var (x, y, z) = EllipsoidMath.EllipsoidToGeocentric(lat, lon, height, GRS67.Parameters);
        var (lat2, lon2, h2) = EllipsoidMath.GeocentricToEllipsoid(x, y, z, GRS67.Parameters);

        Assert.Equal(lat, lat2, precision: 9);
        Assert.Equal(lon, lon2, precision: 9);
        Assert.Equal(height, h2, 0.001);
    }

    [Fact]
    public void DecimalDegreesToDms_KnownValue()
    {
        var (deg, min, sec) = EllipsoidMath.DecimalDegreesToDms(47.5);
        Assert.Equal(47, deg, precision: 0);
        Assert.Equal(30, min, precision: 0);
        Assert.Equal(0.0, sec, 0.001);
    }

    [Fact]
    public void DmsToDecimalDegrees_RoundTrip()
    {
        var original = 19.123456;
        var (deg, min, sec) = EllipsoidMath.DecimalDegreesToDms(original);
        var result = EllipsoidMath.DmsToDecimalDegrees(deg, min, sec);
        Assert.Equal(original, result, 8);
    }
}
