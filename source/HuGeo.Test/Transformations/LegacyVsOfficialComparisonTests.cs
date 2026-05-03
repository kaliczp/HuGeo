using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

public class LegacyVsOfficialComparisonTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TransformationContext _official = null!;
    private TecaTransformationContext _legacy = null!;

    public LegacyVsOfficialComparisonTests(ITestOutputHelper output) => _output = output;

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

        _legacy = new TecaTransformationContext(new TecaGridLoader().Load());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void OfficialBeatsLegacy_OnOfficialForwardFixture()
    {
        var points = LoadForwardFixture();
        Assert.True(points.Count > 0, "Forward fixture is empty");

        var officialErrors = new List<double>();
        var legacyErrors = new List<double>();

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var official = _official.TransformHd72ToEtrs89(hd72);
                var legacy = _legacy.TransformHd72ToWgs84(hd72);

                officialErrors.Add(HaversineMeters(official.Latitude, official.Longitude, pt.ExpectedLat, pt.ExpectedLon));
                legacyErrors.Add(HaversineMeters(legacy.Latitude, legacy.Longitude, pt.ExpectedLat, pt.ExpectedLon));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine("=== Legacy vs Official: forward fixture ===");
        _output.WriteLine($"  official avg: {officialErrors.Average():G17} m");
        _output.WriteLine($"  official max: {officialErrors.Max():G17} m");
        _output.WriteLine($"  legacy avg: {legacyErrors.Average():G17} m");
        _output.WriteLine($"  legacy max: {legacyErrors.Max():G17} m");

        Assert.True(officialErrors.Count > 0, "No comparable forward points were evaluated");
        Assert.True(officialErrors.Average() < legacyErrors.Average(), "Official forward path should beat legacy average error");
        Assert.True(officialErrors.Max() < legacyErrors.Max(), "Official forward path should beat legacy max error");
    }

    [Fact]
    public void OfficialBeatsLegacy_OnOfficialReverseFixture()
    {
        var points = LoadReverseFixture();
        Assert.True(points.Count > 0, "Reverse fixture is empty");

        var officialErrors = new List<double>();
        var legacyErrors = new List<double>();

        foreach (var pt in points)
        {
            try
            {
                var etrs89 = new Etrs89Coordinate(pt.ExpectedLat, pt.ExpectedLon, pt.ExpectedH);
                var official = _official.TransformEtrs89ToHd72(etrs89);
                var legacy = _legacy.TransformWgs84ToHd72(new Wgs84Coordinate(pt.ExpectedLat, pt.ExpectedLon, pt.ExpectedH));

                officialErrors.Add(System.Math.Sqrt(
                    System.Math.Pow(official.Easting - pt.EovY, 2) +
                    System.Math.Pow(official.Northing - pt.EovX, 2)));
                legacyErrors.Add(System.Math.Sqrt(
                    System.Math.Pow(legacy.Easting - pt.EovY, 2) +
                    System.Math.Pow(legacy.Northing - pt.EovX, 2)));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine("=== Legacy vs Official: reverse fixture ===");
        _output.WriteLine($"  official avg: {officialErrors.Average():G17} m");
        _output.WriteLine($"  official max: {officialErrors.Max():G17} m");
        _output.WriteLine($"  legacy avg: {legacyErrors.Average():G17} m");
        _output.WriteLine($"  legacy max: {legacyErrors.Max():G17} m");

        Assert.True(officialErrors.Count > 0, "No comparable reverse points were evaluated");
        Assert.True(officialErrors.Average() < legacyErrors.Average(), "Official reverse path should beat legacy average error");
        Assert.True(officialErrors.Max() < legacyErrors.Max(), "Official reverse path should beat legacy max error");
    }

    private record ForwardPoint(double EovY, double EovX, double EovH, double ExpectedLat, double ExpectedLon, double ExpectedH);
    private record ReversePoint(double ExpectedLat, double ExpectedLon, double ExpectedH, double EovY, double EovX, double EovH);

    private static double ParseHu(string s) => TestHelpers.ParseHu(s);

    private static List<ForwardPoint> LoadForwardFixture()
    {
        var assembly = typeof(LegacyVsOfficialComparisonTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.eov-etrs89-official.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<ForwardPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new ForwardPoint(
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3]),
                ParseHu(parts[4]),
                ParseHu(parts[5]),
                ParseHu(parts[6])));
        }

        return points;
    }

    private static List<ReversePoint> LoadReverseFixture()
    {
        var assembly = typeof(LegacyVsOfficialComparisonTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.etrs89-eov-official.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<ReversePoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new ReversePoint(
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3]),
                ParseHu(parts[4]),
                ParseHu(parts[5]),
                ParseHu(parts[6])));
        }

        return points;
    }

    private static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg) =>
        TestHelpers.HaversineMeters(lat1Deg, lon1Deg, lat2Deg, lon2Deg);
}
