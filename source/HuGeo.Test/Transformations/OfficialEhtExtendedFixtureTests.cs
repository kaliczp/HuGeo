using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

/// <summary>
/// Extended accuracy tests using 2000-point stratified fixture across official grid bounds.
/// Provides deeper statistical validation: P95, P99 percentiles, outlier detection, boundary behavior.
/// </summary>
public class OfficialEhtExtendedFixtureTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TransformationContext _context = null!;

    public OfficialEhtExtendedFixtureTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        await repo.InitializeAsync();

        _context = new TransformationContext(
            TransformationMode.OfficialGrid,
            repo.CorrectionProvider.GetHd72Corrections,
            repo.CorrectionProvider.GetWgs84Corrections,
            repo.CorrectionProvider.GetOfficialCorrections,
            repo.CorrectionProvider.GetOfficialHeightCorrection);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ExtendedFixture_EovToEtrs89_ValidatesFullCoverage()
    {
        var points = LoadExtendedOfficialPoints();
        Assert.True(points.Count >= 1900, "Extended fixture should have ~2000 points");

        var latErrors = new List<double>();
        var lonErrors = new List<double>();
        var heightErrors = new List<double>();
        var horizErrors = new List<double>();
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var etrs89 = _context.TransformHd72ToEtrs89(hd72);

                latErrors.Add(System.Math.Abs(etrs89.Latitude - pt.ExpectedLat));
                lonErrors.Add(System.Math.Abs(etrs89.Longitude - pt.ExpectedLon));
                heightErrors.Add(System.Math.Abs(etrs89.Height - pt.ExpectedH));
                horizErrors.Add(HaversineMeters(etrs89.Latitude, etrs89.Longitude, pt.ExpectedLat, pt.ExpectedLon));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Extended Forward: EOV -> ETRS89 (2000 points) ===");
        _output.WriteLine($"  covered: {covered} / {points.Count}");
        if (covered > 0)
        {
            var sortedHoriz = horizErrors.OrderBy(e => e).ToList();
            _output.WriteLine($"  lat avg: {latErrors.Average():G17} deg");
            _output.WriteLine($"  lat max: {latErrors.Max():G17} deg");
            _output.WriteLine($"  lon avg: {lonErrors.Average():G17} deg");
            _output.WriteLine($"  lon max: {lonErrors.Max():G17} deg");
            _output.WriteLine($"  height avg: {heightErrors.Average():G17} m");
            _output.WriteLine($"  height max: {heightErrors.Max():G17} m");
            _output.WriteLine($"  2D avg: {horizErrors.Average():G17} m");
            _output.WriteLine($"  2D max: {horizErrors.Max():G17} m");
            _output.WriteLine($"  2D p50: {Percentile(horizErrors, 0.50):G17} m");
            _output.WriteLine($"  2D p95: {Percentile(horizErrors, 0.95):G17} m");
            _output.WriteLine($"  2D p99: {Percentile(horizErrors, 0.99):G17} m");

            // Outlier analysis
            var q3 = Percentile(horizErrors, 0.75);
            var q1 = Percentile(horizErrors, 0.25);
            var iqr = q3 - q1;
            var outlierThreshold = q3 + 1.5 * iqr;
            var outliers = horizErrors.Count(e => e > outlierThreshold);
            _output.WriteLine($"  outliers (>1.5×IQR): {outliers} ({100.0 * outliers / horizErrors.Count:F2}%)");
        }

        Assert.True(covered >= points.Count * 0.95, $"Extended forward coverage too low: {covered}/{points.Count}");
        Assert.True(latErrors.Count > 0, "No forward points were evaluated");
        Assert.True(latErrors.Max() <= 3e-7, $"Extended forward latitude max error too large: {latErrors.Max():G17} deg");
        Assert.True(lonErrors.Max() <= 5e-7, $"Extended forward longitude max error too large: {lonErrors.Max():G17} deg");
        Assert.True(heightErrors.Max() <= 0.01, $"Extended forward height max error too large: {heightErrors.Max():G17} m");
        Assert.True(horizErrors.Max() <= 0.05, $"Extended forward horizontal max error too large: {horizErrors.Max():G17} m");
    }

    [Fact]
    public void ExtendedFixture_Etrs89ToEov_ValidatesReverseApproximation()
    {
        var points = LoadReverseExtendedOfficialPoints();
        Assert.True(points.Count >= 1900, "Extended reverse fixture should have ~2000 points");

        var yErrors = new List<double>();
        var xErrors = new List<double>();
        var hErrors = new List<double>();
        var planarErrors = new List<double>();
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var etrs89 = new Etrs89Coordinate(pt.Latitude, pt.Longitude, pt.Height);
                var hd72 = _context.TransformEtrs89ToHd72(etrs89);

                yErrors.Add(System.Math.Abs(hd72.Easting - pt.EovY));
                xErrors.Add(System.Math.Abs(hd72.Northing - pt.EovX));
                hErrors.Add(System.Math.Abs(hd72.Height - pt.EovH));
                planarErrors.Add(System.Math.Sqrt(
                    System.Math.Pow(hd72.Easting - pt.EovY, 2) +
                    System.Math.Pow(hd72.Northing - pt.EovX, 2)));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Extended Reverse: ETRS89 -> EOV (2000 points) ===");
        _output.WriteLine($"  covered: {covered} / {points.Count}");
        if (covered > 0)
        {
            _output.WriteLine($"  y avg: {yErrors.Average():G17} m");
            _output.WriteLine($"  y max: {yErrors.Max():G17} m");
            _output.WriteLine($"  x avg: {xErrors.Average():G17} m");
            _output.WriteLine($"  x max: {xErrors.Max():G17} m");
            _output.WriteLine($"  h avg: {hErrors.Average():G17} m");
            _output.WriteLine($"  h max: {hErrors.Max():G17} m");
            _output.WriteLine($"  2D avg: {planarErrors.Average():G17} m");
            _output.WriteLine($"  2D max: {planarErrors.Max():G17} m");
            _output.WriteLine($"  2D p50: {Percentile(planarErrors, 0.50):G17} m");
            _output.WriteLine($"  2D p95: {Percentile(planarErrors, 0.95):G17} m");
            _output.WriteLine($"  2D p99: {Percentile(planarErrors, 0.99):G17} m");

            // Outlier analysis
            var q3 = Percentile(planarErrors, 0.75);
            var q1 = Percentile(planarErrors, 0.25);
            var iqr = q3 - q1;
            var outlierThreshold = q3 + 1.5 * iqr;
            var outliers = planarErrors.Count(e => e > outlierThreshold);
            _output.WriteLine($"  outliers (>1.5×IQR): {outliers} ({100.0 * outliers / planarErrors.Count:F2}%)");
        }

        Assert.True(covered >= points.Count * 0.95, $"Extended reverse coverage too low: {covered}/{points.Count}");
        Assert.True(yErrors.Count > 0, "No reverse points were evaluated");
        Assert.True(yErrors.Max() <= 0.05, $"Extended reverse easting max error too large: {yErrors.Max():G17} m");
        Assert.True(xErrors.Max() <= 0.05, $"Extended reverse northing max error too large: {xErrors.Max():G17} m");
        Assert.True(hErrors.Max() <= 0.01, $"Extended reverse height max error too large: {hErrors.Max():G17} m");
        Assert.True(planarErrors.Max() <= 0.05, $"Extended reverse planar max error too large: {planarErrors.Max():G17} m");
    }

    [Fact]
    public void ExtendedFixture_RoundTripConsistency()
    {
        var forwardPoints = LoadExtendedOfficialPoints();
        var roundTripErrors = new List<double>();
        var covered = 0;

        foreach (var pt in forwardPoints)
        {
            try
            {
                // Forward: EOV -> ETRS89
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var etrs89 = _context.TransformHd72ToEtrs89(hd72);

                // Reverse: ETRS89 -> EOV
                var hd72Back = _context.TransformEtrs89ToHd72(etrs89);

                // Check round-trip error (EOV -> ETRS89 -> EOV)
                var yError = System.Math.Abs(hd72Back.Easting - pt.EovY);
                var xError = System.Math.Abs(hd72Back.Northing - pt.EovX);
                var roundTripError = System.Math.Sqrt(yError * yError + xError * xError);
                roundTripErrors.Add(roundTripError);
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Extended Round-Trip (EOV -> ETRS89 -> EOV) ===");
        _output.WriteLine($"  points tested: {covered}");
        if (covered > 0)
        {
            _output.WriteLine($"  round-trip avg: {roundTripErrors.Average():G17} m");
            _output.WriteLine($"  round-trip max: {roundTripErrors.Max():G17} m");
            _output.WriteLine($"  round-trip p95: {Percentile(roundTripErrors, 0.95):G17} m");
        }

        Assert.True(covered > 0, "Round-trip test should evaluate some points");
        Assert.True(roundTripErrors.Max() <= 0.05, $"Round-trip error too large: {roundTripErrors.Max():G17} m");
    }

    private record ExtendedOfficialPoint(
        string PointNumber,
        double EovY,
        double EovX,
        double EovH,
        double ExpectedLat,
        double ExpectedLon,
        double ExpectedH);

    private record ReverseExtendedOfficialPoint(
        string PointNumber,
        double Latitude,
        double Longitude,
        double Height,
        double EovY,
        double EovX,
        double EovH);

    private static double ParseHu(string s) => TestHelpers.ParseHu(s);

    private static List<ExtendedOfficialPoint> LoadExtendedOfficialPoints()
    {
        var assembly = typeof(OfficialEhtExtendedFixtureTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.eov-etrs89-official-extended.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<ExtendedOfficialPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new ExtendedOfficialPoint(
                parts[0].Trim(),
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3]),
                ParseHu(parts[4]),
                ParseHu(parts[5]),
                ParseHu(parts[6])));
        }

        return points;
    }

    private static List<ReverseExtendedOfficialPoint> LoadReverseExtendedOfficialPoints()
    {
        var assembly = typeof(OfficialEhtExtendedFixtureTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.etrs89-eov-official-extended.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<ReverseExtendedOfficialPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new ReverseExtendedOfficialPoint(
                parts[0].Trim(),
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3]),
                ParseHu(parts[4]),
                ParseHu(parts[5]),
                ParseHu(parts[6])));
        }

        return points;
    }

    private static double Percentile(List<double> values, double p) =>
        TestHelpers.Percentile(values, p);

    private static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg) =>
        TestHelpers.HaversineMeters(lat1Deg, lon1Deg, lat2Deg, lon2Deg);
}
