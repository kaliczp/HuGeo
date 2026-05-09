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
        var points = OfficialFixtureData.LoadExtendedForwardPoints();
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

        _output.WriteLine("=== Legacy vs Official: nationwide forward fixture ===");
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
        var points = OfficialFixtureData.LoadExtendedReversePoints();
        Assert.True(points.Count > 0, "Reverse fixture is empty");

        var officialErrors = new List<double>();
        var legacyErrors = new List<double>();

        foreach (var pt in points)
        {
            try
            {
                var etrs89 = new Etrs89Coordinate(pt.Latitude, pt.Longitude, pt.Height);
                var official = _official.TransformEtrs89ToHd72(etrs89);
                var legacy = _legacy.TransformWgs84ToHd72(new Wgs84Coordinate(pt.Latitude, pt.Longitude, pt.Height));

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

        _output.WriteLine("=== Legacy vs Official: nationwide reverse fixture ===");
        _output.WriteLine($"  official avg: {officialErrors.Average():G17} m");
        _output.WriteLine($"  official max: {officialErrors.Max():G17} m");
        _output.WriteLine($"  legacy avg: {legacyErrors.Average():G17} m");
        _output.WriteLine($"  legacy max: {legacyErrors.Max():G17} m");

        Assert.True(officialErrors.Count > 0, "No comparable reverse points were evaluated");
        Assert.True(officialErrors.Average() < legacyErrors.Average(), "Official reverse path should beat legacy average error");
        Assert.True(officialErrors.Max() < legacyErrors.Max(), "Official reverse path should beat legacy max error");
    }

    private static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg) =>
        TestHelpers.HaversineMeters(lat1Deg, lon1Deg, lat2Deg, lon2Deg);
}
