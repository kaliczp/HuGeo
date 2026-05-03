using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;

namespace HuGeo.Tests.Transformations;

/// <summary>
/// Regressziós tesztek az EHT (Egységes Horizontális Transzformáció) referencia pontokkal.
/// Forrás: eov-wgs84-eht-test.txt
/// Formátum: Eov Y (Easting), Eov X (Northing), Eov H → WGS84 Fi (Lat), La (Lon), H
/// </summary>
public class EhtRegressionTests
{
    private static List<EhtTestData.EhtTestPoint> LoadTestPoints() =>
        EhtTestData.LoadEhtPoints();

    [Fact]
    public void EhtTestFile_LoadsSuccessfully()
    {
        var points = LoadTestPoints();
        Assert.True(points.Count > 50, $"Expected >50 test points, got {points.Count}");
    }

    [Fact]
    public void Hd72ToWgs84_HelmertOnly_AllPoints_AllWithin005Degrees()
    {
        // Helmert-only: ~0.05° (~5km) tolerancia — alap Helmert pontossága rácsos javítás nélkül
        var ctx = new TransformationContext(TransformationMode.HelmertOnly);
        var points = LoadTestPoints();
        var errors = new List<string>();

        foreach (var pt in points)
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = ctx.TransformHd72ToWgs84(hd72);

            var latErr = System.Math.Abs(wgs84.Latitude - pt.ExpectedLat);
            var lonErr = System.Math.Abs(wgs84.Longitude - pt.ExpectedLon);

            if (latErr > 0.05 || lonErr > 0.05)
                errors.Add($"Y={pt.EovY} X={pt.EovX}: LatErr={latErr:F6}° LonErr={lonErr:F6}°");
        }

        Assert.True(errors.Count == 0,
            $"{errors.Count}/{points.Count} points outside 0.05° tolerance:\n{string.Join("\n", errors.Take(5))}");
    }

    [Fact]
    public void Hd72ToWgs84_HelmertOnly_AllPoints_LatitudeWithin005Degrees()
    {
        // Helmert 7-param: elvárható ~0.001° (~100m) pontosság
        var ctx = new TransformationContext(TransformationMode.HelmertOnly);
        var points = LoadTestPoints();

        var latErrors = points.Select(pt =>
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = ctx.TransformHd72ToWgs84(hd72);
            return System.Math.Abs(wgs84.Latitude - pt.ExpectedLat);
        }).ToList();

        var maxLatError = latErrors.Max();
        var avgLatError = latErrors.Average();

        Assert.True(maxLatError < 0.005,
            $"Max latitude error {maxLatError:F6}° exceeds 0.005° (500m). " +
            $"Avg: {avgLatError:F6}°");
    }

    [Fact]
    public void Hd72ToWgs84_HelmertOnly_AllPoints_LongitudeWithin001Degrees()
    {
        var ctx = new TransformationContext(TransformationMode.HelmertOnly);
        var points = LoadTestPoints();

        var lonErrors = points.Select(pt =>
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = ctx.TransformHd72ToWgs84(hd72);
            return System.Math.Abs(wgs84.Longitude - pt.ExpectedLon);
        }).ToList();

        var maxLonError = lonErrors.Max();
        var avgLonError = lonErrors.Average();

        Assert.True(maxLonError < 0.005,
            $"Max longitude error {maxLonError:F6}° exceeds 0.005° (500m). " +
            $"Avg: {avgLonError:F6}°");
    }

    [Fact]
    public async Task OfficialGrid_Hd72ToWgs84_FirstCoveredKnownPoint_MatchesExpected()
    {
        // Első EHT pont: Y=416000, X=186000, H=417 → Lat=46.976737976, Lon=15.971119881
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        await repo.InitializeAsync();
        var ctx = new TransformationContext(
            TransformationMode.OfficialGrid,
            repo.CorrectionProvider.GetHd72Corrections,
            repo.CorrectionProvider.GetWgs84Corrections,
            repo.CorrectionProvider.GetOfficialCorrections,
            repo.CorrectionProvider.GetOfficialHeightCorrection);
        foreach (var pt in LoadTestPoints())
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var wgs84 = ctx.TransformHd72ToWgs84(hd72);

                var horizontalError = TestHelpers.HaversineMeters(
                    pt.ExpectedLat,
                    pt.ExpectedLon,
                    wgs84.Latitude,
                    wgs84.Longitude);

                Assert.True(horizontalError <= 0.5, $"Known EHT point horizontal error {horizontalError:G17} m > 0.5 m");
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
            }
        }

        Assert.Fail("No EHT reference point was covered by the official grid.");
    }
}
