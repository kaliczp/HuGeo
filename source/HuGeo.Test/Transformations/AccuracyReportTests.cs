using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

/// <summary>
/// Pontossági riport: összehasonlítja a Helmert-only és Grid-based módokat.
/// Ezek a tesztek az output-ban mutatják a statisztikákat.
/// </summary>
public class AccuracyReportTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ILegacyCoordinateTransformer _gridTransformer = null!;

    public AccuracyReportTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        _gridTransformer = new CoordinateTransformer(repo);
        await _gridTransformer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void AccuracyReport_HelmertVsGrid_AllEhtPoints()
    {
        var points = LoadEhtPoints();
        Assert.True(points.Count > 0);

        var helmertCtx = new TransformationContext(TransformationMode.HelmertOnly);

        var helmertLatErrors = new List<double>();
        var helmertLonErrors = new List<double>();
        var gridLatErrors = new List<double>();
        var gridLonErrors = new List<double>();

        foreach (var pt in points)
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);

            var helmertResult = helmertCtx.TransformHd72ToWgs84(hd72);
            helmertLatErrors.Add(System.Math.Abs(helmertResult.Latitude - pt.ExpectedLat));
            helmertLonErrors.Add(System.Math.Abs(helmertResult.Longitude - pt.ExpectedLon));

            var gridResult = _gridTransformer.Transform(hd72);
            gridLatErrors.Add(System.Math.Abs(gridResult.Latitude - pt.ExpectedLat));
            gridLonErrors.Add(System.Math.Abs(gridResult.Longitude - pt.ExpectedLon));
        }

        // ~111111 m/° szélességben, ~73000 m/° hosszúságban (47°-on)
        double latMperDeg = 111111.0;
        double lonMperDeg = 75860.0;

        _output.WriteLine($"=== Pontossági riport ({points.Count} EHT pont) ===");
        _output.WriteLine("");
        _output.WriteLine("--- Helmert-only ---");
        _output.WriteLine($"  Lat hiba  max: {helmertLatErrors.Max():F6}°  ({helmertLatErrors.Max() * latMperDeg:F1} m)");
        _output.WriteLine($"  Lat hiba  avg: {helmertLatErrors.Average():F6}°  ({helmertLatErrors.Average() * latMperDeg:F1} m)");
        _output.WriteLine($"  Lon hiba  max: {helmertLonErrors.Max():F6}°  ({helmertLonErrors.Max() * lonMperDeg:F1} m)");
        _output.WriteLine($"  Lon hiba  avg: {helmertLonErrors.Average():F6}°  ({helmertLonErrors.Average() * lonMperDeg:F1} m)");
        _output.WriteLine("");
        _output.WriteLine("--- Grid-based (IDW rácsos javítással) ---");
        _output.WriteLine($"  Lat hiba  max: {gridLatErrors.Max():F6}°  ({gridLatErrors.Max() * latMperDeg:F3} m)");
        _output.WriteLine($"  Lat hiba  avg: {gridLatErrors.Average():F6}°  ({gridLatErrors.Average() * latMperDeg:F3} m)");
        _output.WriteLine($"  Lon hiba  max: {gridLonErrors.Max():F6}°  ({gridLonErrors.Max() * lonMperDeg:F3} m)");
        _output.WriteLine($"  Lon hiba  avg: {gridLonErrors.Average():F6}°  ({gridLonErrors.Average() * lonMperDeg:F3} m)");

        var sortedLat = gridLatErrors.OrderBy(x => x).ToList();
        var sortedLon = gridLonErrors.OrderBy(x => x).ToList();
        int n = sortedLat.Count;
        _output.WriteLine("");
        _output.WriteLine("--- Grid Lat hiba percentilis (méterben) ---");
        _output.WriteLine($"  p50:  {sortedLat[n/2] * latMperDeg:F3} m");
        _output.WriteLine($"  p75:  {sortedLat[n*3/4] * latMperDeg:F3} m");
        _output.WriteLine($"  p90:  {sortedLat[n*9/10] * latMperDeg:F3} m");
        _output.WriteLine($"  p95:  {sortedLat[n*19/20] * latMperDeg:F3} m");
        _output.WriteLine($"  p99:  {sortedLat[(int)(n*0.99)] * latMperDeg:F3} m");
        _output.WriteLine($"  >0.01m: {gridLatErrors.Count(e => e * latMperDeg > 0.01)}");
        _output.WriteLine($"  >0.10m: {gridLatErrors.Count(e => e * latMperDeg > 0.10)}");
        _output.WriteLine($"  >0.50m: {gridLatErrors.Count(e => e * latMperDeg > 0.50)}");
        _output.WriteLine($"  >1.00m: {gridLatErrors.Count(e => e * latMperDeg > 1.00)}");

        // Grid-nek jobbnak kell lennie, mint Helmert-only
        Assert.True(gridLatErrors.Average() < helmertLatErrors.Average(),
            "Grid mode should be more accurate than Helmert-only");
        Assert.True(gridLonErrors.Average() < helmertLonErrors.Average(),
            "Grid mode should be more accurate than Helmert-only (lon)");
    }

    private static List<EhtTestData.EhtTestPoint> LoadEhtPoints() =>
        EhtTestData.LoadEhtPoints();
}
