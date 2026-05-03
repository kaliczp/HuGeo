using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

public class TecaAccuracyTests
{
    private readonly ITestOutputHelper _output;
    private readonly TecaTransformationContext _teca;

    public TecaAccuracyTests(ITestOutputHelper output)
    {
        _output = output;
        var grid = new TecaGridLoader().Load();
        _teca = new TecaTransformationContext(grid);
    }

    [Fact]
    public void TecaGrid_Loads_Successfully()
    {
        var grid = new TecaGridLoader().Load();
        Assert.NotNull(grid);
        // Ellenőrzés: Magyarország közepe benne van a rácsban
        var corr = grid.Interpolate(47.5, 19.0);
        Assert.NotNull(corr);
        _output.WriteLine($"Budapest közepe (47.5°N, 19.0°E): dx={corr!.Value.Dx:F4}m, dy={corr.Value.Dy:F4}m, dh={corr.Value.Dh:F4}m");
    }

    [Fact]
    public void Teca_AccuracyReport_AllEhtPoints()
    {
        var points = LoadEhtPoints();
        Assert.True(points.Count > 0);

        var latErrors = new List<double>();
        var lonErrors = new List<double>();
        var horizontalErrors = new List<double>();

        foreach (var pt in points)
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = _teca.TransformHd72ToWgs84(hd72);
            var latError = System.Math.Abs(wgs84.Latitude - pt.ExpectedLat);
            var lonError = System.Math.Abs(wgs84.Longitude - pt.ExpectedLon);
            latErrors.Add(latError);
            lonErrors.Add(lonError);
            horizontalErrors.Add(System.Math.Sqrt(
                System.Math.Pow(latError * 111111.0, 2) +
                System.Math.Pow(lonError * 75860.0, 2)));
        }

        double latMperDeg = 111111.0;
        double lonMperDeg = 75860.0;

        var sortedLat = latErrors.OrderBy(x => x).ToList();
        int n = sortedLat.Count;

        _output.WriteLine($"=== TECA pontossági riport ({n} EHT pont) ===");
        _output.WriteLine($"  Lat avg:  {latErrors.Average():F7}°  ({latErrors.Average() * latMperDeg:F4} m)");
        _output.WriteLine($"  Lat max:  {latErrors.Max():F7}°  ({latErrors.Max() * latMperDeg:F4} m)");
        _output.WriteLine($"  Lon avg:  {lonErrors.Average():F7}°  ({lonErrors.Average() * lonMperDeg:F4} m)");
        _output.WriteLine($"  Lon max:  {lonErrors.Max():F7}°  ({lonErrors.Max() * lonMperDeg:F4} m)");
        _output.WriteLine($"  Lat p50:  {sortedLat[n/2] * latMperDeg:F4} m");
        _output.WriteLine($"  Lat p90:  {sortedLat[n*9/10] * latMperDeg:F4} m");
        _output.WriteLine($"  Lat p99:  {sortedLat[(int)(n*0.99)] * latMperDeg:F4} m");
        _output.WriteLine($"  >1cm:  {latErrors.Count(e => e * latMperDeg > 0.01)}");
        _output.WriteLine($"  >10cm: {latErrors.Count(e => e * latMperDeg > 0.10)}");
        _output.WriteLine($"  >50cm: {latErrors.Count(e => e * latMperDeg > 0.50)}");

        Assert.True(horizontalErrors.Max() < 1.0, $"TECA max horizontal error {horizontalErrors.Max():F4} m >= 1 m");
    }

    [Fact]
    public void Teca_WgsToHd72_IsAccurate()
    {
        // WGS84 → EOV irány tesztelése (fordított EHT: WGS84 bemenetre EOV kimenetet várunk)
        // Ellenőrzés round-trip-pel
        var testPoints = new[]
        {
            new Hd72Coordinate(650000, 250000, 150),
            new Hd72Coordinate(416000, 186000, 417),
            new Hd72Coordinate(700000, 300000, 200),
        };

        foreach (var original in testPoints)
        {
            var wgs84 = _teca.TransformHd72ToWgs84(original);
            var roundTrip = _teca.TransformWgs84ToHd72(wgs84);

            var yErr = System.Math.Abs(original.Easting  - roundTrip.Easting);
            var xErr = System.Math.Abs(original.Northing - roundTrip.Northing);

            _output.WriteLine($"  Pont ({original.Easting},{original.Northing}): Y-hiba={yErr:F3}m, X-hiba={xErr:F3}m");

            Assert.True(yErr < 0.1, $"Y round-trip hiba {yErr:F3}m > 10cm");
            Assert.True(xErr < 0.1, $"X round-trip hiba {xErr:F3}m > 10cm");
        }
    }

    private static List<EhtTestData.EhtTestPoint> LoadEhtPoints() =>
        EhtTestData.LoadEhtPoints();
}
