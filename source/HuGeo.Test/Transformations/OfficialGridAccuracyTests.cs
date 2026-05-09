using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

public class OfficialGridAccuracyTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TransformationContext _official = null!;
    private TecaTransformationContext _teca = null!;

    public OfficialGridAccuracyTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        await repo.InitializeAsync();

        _official = new TransformationContext(
            TransformationMode.OfficialGrid,
            repo.CorrectionProvider.GetHd72Corrections,
            repo.CorrectionProvider.GetWgs84Corrections,
            repo.CorrectionProvider.GetOfficialCorrections,
            repo.CorrectionProvider.GetOfficialHeightCorrection);

        _teca = new TecaTransformationContext(new TecaGridLoader().Load());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void OfficialForwardBenchmark_UsesNationwideOfficialFixtureOnly()
    {
        var points = OfficialFixtureData.LoadExtendedForwardPoints();
        Assert.True(points.Count >= 1900, $"Nationwide official forward fixture should contain ~2000 points, got {points.Count}");

        var officialErrors = new List<double>(points.Count);
        var tecaErrors = new List<double>(points.Count);
        var officialHeightErrors = new List<double>(points.Count);
        var tecaHeightErrors = new List<double>(points.Count);
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var official = _official.TransformHd72ToEtrs89(hd72);
                var teca = _teca.TransformHd72ToWgs84(hd72);

                officialErrors.Add(HaversineMeters(official.Latitude, official.Longitude, pt.ExpectedLat, pt.ExpectedLon));
                tecaErrors.Add(HaversineMeters(teca.Latitude, teca.Longitude, pt.ExpectedLat, pt.ExpectedLon));
                officialHeightErrors.Add(System.Math.Abs(official.Height - pt.ExpectedH));
                tecaHeightErrors.Add(System.Math.Abs(teca.Height - pt.ExpectedH));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Nationwide official forward benchmark ({covered}/{points.Count} covered) ===");
        WriteDistanceStats("official", officialErrors);
        WriteHeightStats("official", officialHeightErrors);
        WriteDistanceStats("teca", tecaErrors);
        WriteHeightStats("teca", tecaHeightErrors);

        Assert.True(covered >= points.Count * 0.95, $"Forward coverage too low: {covered}/{points.Count}");
        Assert.True(officialErrors.Count > 0, "No nationwide forward points were evaluated");
        Assert.True(officialErrors.Average() < tecaErrors.Average(), "Official forward average error should beat TECA on nationwide official fixture");
        Assert.True(officialErrors.Max() < tecaErrors.Max(), "Official forward max error should beat TECA on nationwide official fixture");
    }

    [Fact]
    public void OfficialReverseBenchmark_UsesNationwideOfficialFixtureOnly()
    {
        var points = OfficialFixtureData.LoadExtendedReversePoints();
        Assert.True(points.Count >= 1900, $"Nationwide official reverse fixture should contain ~2000 points, got {points.Count}");

        var officialErrors = new List<double>(points.Count);
        var tecaErrors = new List<double>(points.Count);
        var officialHeightErrors = new List<double>(points.Count);
        var tecaHeightErrors = new List<double>(points.Count);
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var etrs89 = new Etrs89Coordinate(pt.Latitude, pt.Longitude, pt.Height);
                var official = _official.TransformEtrs89ToHd72(etrs89);
                var teca = _teca.TransformWgs84ToHd72(new Wgs84Coordinate(pt.Latitude, pt.Longitude, pt.Height));

                officialErrors.Add(PlanarErrorMeters(official.Easting, official.Northing, pt.EovY, pt.EovX));
                tecaErrors.Add(PlanarErrorMeters(teca.Easting, teca.Northing, pt.EovY, pt.EovX));
                officialHeightErrors.Add(System.Math.Abs(official.Height - pt.EovH));
                tecaHeightErrors.Add(System.Math.Abs(teca.Height - pt.EovH));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Nationwide official reverse benchmark ({covered}/{points.Count} covered) ===");
        WriteDistanceStats("official", officialErrors);
        WriteHeightStats("official", officialHeightErrors);
        WriteDistanceStats("teca", tecaErrors);
        WriteHeightStats("teca", tecaHeightErrors);

        Assert.True(covered >= points.Count * 0.95, $"Reverse coverage too low: {covered}/{points.Count}");
        Assert.True(officialErrors.Count > 0, "No nationwide reverse points were evaluated");
        Assert.True(officialErrors.Average() < tecaErrors.Average(), "Official reverse average error should beat TECA on nationwide official fixture");
        Assert.True(officialErrors.Max() < tecaErrors.Max(), "Official reverse max error should beat TECA on nationwide official fixture");
    }

    private void WriteDistanceStats(string label, List<double> errors)
    {
        _output.WriteLine($"  {label} 2D avg: {errors.Average():G17} m");
        _output.WriteLine($"  {label} 2D max: {errors.Max():G17} m");
        _output.WriteLine($"  {label} 2D p95: {Percentile(errors, 0.95):G17} m");
        _output.WriteLine($"  {label} 2D p99: {Percentile(errors, 0.99):G17} m");
    }

    private void WriteHeightStats(string label, List<double> errors)
    {
        _output.WriteLine($"  {label} H avg: {errors.Average():G17} m");
        _output.WriteLine($"  {label} H max: {errors.Max():G17} m");
    }

    private static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg) =>
        TestHelpers.HaversineMeters(lat1Deg, lon1Deg, lat2Deg, lon2Deg);

    private static double PlanarErrorMeters(double actualY, double actualX, double expectedY, double expectedX) =>
        System.Math.Sqrt(System.Math.Pow(actualY - expectedY, 2) + System.Math.Pow(actualX - expectedX, 2));

    private static double Percentile(List<double> values, double p) =>
        TestHelpers.Percentile(values, p);
}
