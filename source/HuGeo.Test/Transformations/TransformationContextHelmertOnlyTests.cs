using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;

namespace HuGeo.Tests.Transformations;

/// <summary>
/// Helmert-only tesztek (rácsos javítás nélkül) — méri az alap matematikai pontosságot.
/// Elvárt pontosság: ~1-3 arcsec (~30-100m) a rácsos javítás nélkül.
/// </summary>
public class TransformationContextHelmertOnlyTests
{
    private readonly TransformationContext _ctx = new(TransformationMode.HelmertOnly);

    [Fact]
    public void Hd72ToWgs84_BudapestArea_LatitudeInRange()
    {
        // Budapest-környék EOV: Y=649000, X=239000
        var hd72 = new Hd72Coordinate(649000, 239000, 100);
        var wgs84 = _ctx.TransformHd72ToWgs84(hd72);

        Assert.InRange(wgs84.Latitude, 47.0, 48.0);
        Assert.InRange(wgs84.Longitude, 18.5, 19.5);
    }

    [Fact]
    public void Wgs84ToHd72_BudapestArea_EovInRange()
    {
        var wgs84 = new Wgs84Coordinate(47.5, 19.0, 100);
        var hd72 = _ctx.TransformWgs84ToHd72(wgs84);

        Assert.InRange(hd72.Easting, 640000, 660000);
        Assert.InRange(hd72.Northing, 230000, 250000);
    }

    [Fact]
    public void Hd72ToWgs84_ThenBack_RoundTripWithinTolerance()
    {
        var original = new Hd72Coordinate(650000, 250000, 150);
        var wgs84 = _ctx.TransformHd72ToWgs84(original);
        var back = _ctx.TransformWgs84ToHd72(wgs84);

        // Helmert-only round-trip: ~1m tolerance
        Assert.Equal(original.Easting, back.Easting, 1);
        Assert.Equal(original.Northing, back.Northing, 1);
        Assert.Equal(original.Height, back.Height, 1);
    }

    [Theory]
    [InlineData(416000, 186000)]
    [InlineData(650000, 200000)]
    [InlineData(750000, 300000)]
    [InlineData(550000, 150000)]
    public void Hd72ToWgs84_MultiplePoints_CoordinatesInHungarianRange(double eovY, double eovX)
    {
        var hd72 = new Hd72Coordinate(eovY, eovX, 100);
        var wgs84 = _ctx.TransformHd72ToWgs84(hd72);

        Assert.InRange(wgs84.Latitude, 45.5, 49.0);
        Assert.InRange(wgs84.Longitude, 15.5, 23.0);
    }
}
