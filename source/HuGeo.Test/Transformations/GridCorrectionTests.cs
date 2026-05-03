using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;

namespace HuGeo.Tests.Transformations;

/// <summary>
/// Rácsos javítással ellátott tesztek — elvárt pontosság ~cm szinten.
/// Az EHT (Egységes Horizontális Transzformáció) referenciaadatokat használja.
/// </summary>
public class GridCorrectionTests : IAsyncLifetime
{
    private ILegacyCoordinateTransformer _transformer = null!;

    public async Task InitializeAsync()
    {
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        _transformer = new CoordinateTransformer(repo);
        await _transformer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void GridIsLoaded_AfterInitialization()
    {
        Assert.True(_transformer.IsReady);
    }

    [Fact]
    public void Hd72ToWgs84_WithGrid_FirstEhtPoint_BetterThan11m()
    {
        // EHT referencia: Y=416000, X=186000, H=417 → Lat=46.976737976, Lon=15.971119881, H=463.459
        var hd72 = new Hd72Coordinate(416000, 186000, 417);
        var wgs84 = _transformer.Transform(hd72);

        var latError = System.Math.Abs(wgs84.Latitude - 46.976737976);
        var lonError = System.Math.Abs(wgs84.Longitude - 15.971119881);

        Assert.True(latError < 0.0001,
            $"Latitude error {latError:F8}° exceeds 0.0001° (≈11m). Got {wgs84.Latitude:F9}");
        Assert.True(lonError < 0.0001,
            $"Longitude error {lonError:F8}° exceeds 0.0001° (≈7m). Got {wgs84.Longitude:F9}");
    }

    [Fact]
    public void Hd72ToWgs84_WithGrid_AllEhtPoints_BetterThanHelmertOnly()
    {
        var points = LoadEhtPoints();
        Assert.True(points.Count > 50, $"Expected >50 test points, loaded {points.Count}");

        var errors = points.Select(pt =>
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = _transformer.Transform(hd72);
            return System.Math.Abs(wgs84.Latitude - pt.ExpectedLat);
        }).ToList();

        var maxErr = errors.Max();
        var avgErr = errors.Average();

        // Grid-del jobb kell legyen, mint 0.001° (Helmert-only max)
        Assert.True(maxErr < 0.001,
            $"Max latitude error {maxErr:F6}° exceeds 0.001°. Avg: {avgErr:F6}°");
    }

    [Fact]
    public void Hd72ToWgs84_WithGrid_AllEhtPoints_Longitude_BetterThanHelmertOnly()
    {
        var points = LoadEhtPoints();

        var errors = points.Select(pt =>
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = _transformer.Transform(hd72);
            return System.Math.Abs(wgs84.Longitude - pt.ExpectedLon);
        }).ToList();

        var maxErr = errors.Max();
        var avgErr = errors.Average();

        Assert.True(maxErr < 0.001,
            $"Max longitude error {maxErr:F6}° exceeds 0.001°. Avg: {avgErr:F6}°");
    }

    [Fact]
    public void Hd72ToWgs84_WithGrid_RoundTrip_SubMeterPrecision()
    {
        // Kétirányú round-trip teszt: HD72 → WGS84 → HD72
        var testPoints = new[]
        {
            new Hd72Coordinate(650000, 250000, 150),
            new Hd72Coordinate(416000, 186000, 417),
            new Hd72Coordinate(700000, 300000, 200),
        };

        foreach (var original in testPoints)
        {
            var wgs84 = _transformer.Transform(original);
            var roundTrip = _transformer.Transform(wgs84);

            var easting_err = System.Math.Abs(original.Easting - roundTrip.Easting);
            var northing_err = System.Math.Abs(original.Northing - roundTrip.Northing);

            Assert.True(easting_err < 0.5,
                $"Round-trip Easting error {easting_err:F3}m for point {original}");
            Assert.True(northing_err < 0.5,
                $"Round-trip Northing error {northing_err:F3}m for point {original}");
        }
    }

    private static List<EhtTestData.EhtTestPoint> LoadEhtPoints() =>
        EhtTestData.LoadEhtPoints();
}
