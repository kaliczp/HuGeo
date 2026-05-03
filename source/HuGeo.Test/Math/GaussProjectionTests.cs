using HuGeo.Core.Math;

namespace HuGeo.Tests.Math;

public class GaussProjectionTests
{
    // EOV convention: Y=Easting (~650000 origin), X=Northing (~200000 origin)

    [Fact]
    public void EovToGrs67_Grs67ToEov_RoundTrip()
    {
        var eovY = 650000.0;
        var eovX = 250000.0;
        var height = 150.0;

        var (lat, lon, h) = GaussProjection.EovToGrs67(eovY, eovX, height);
        var (eovYBack, eovXBack, heightBack) = GaussProjection.Grs67ToEov(lat, lon, h);

        Assert.Equal(eovY, eovYBack, 1);
        Assert.Equal(eovX, eovXBack, 1);
        Assert.Equal(height, heightBack, 0.001);
    }

    [Theory]
    [InlineData(416000.0, 186000.0, 417.0)]
    [InlineData(650000.0, 200000.0, 100.0)]
    [InlineData(750000.0, 300000.0, 250.0)]
    public void EovToGrs67_Grs67ToEov_RoundTrip_MultiplePoints(double eovY, double eovX, double h)
    {
        var (lat, lon, hOut) = GaussProjection.EovToGrs67(eovY, eovX, h);
        var (eovYBack, eovXBack, heightBack) = GaussProjection.Grs67ToEov(lat, lon, hOut);

        Assert.Equal(eovY, eovYBack, 1);
        Assert.Equal(eovX, eovXBack, 1);
        Assert.Equal(h, heightBack, 0.001);
    }

    [Fact]
    public void EovToGrs67_ReturnsLatitudeInHungarianRange()
    {
        // Tipikus magyarországi EOV koordináta: Y=650000, X=250000
        var (lat, lon, _) = GaussProjection.EovToGrs67(650000, 250000, 0);
        var latDeg = EllipsoidMath.RadiansToDegrees(lat);
        var lonDeg = EllipsoidMath.RadiansToDegrees(lon);

        Assert.InRange(latDeg, 45.0, 49.0);
        Assert.InRange(lonDeg, 16.0, 23.0);
    }
}
