using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Ellipsoids;
using HuGeo.Core.Math;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

public class OfficialGridAccuracyTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TransformationContext _context = null!;

    public OfficialGridAccuracyTests(ITestOutputHelper output) => _output = output;

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

    [Fact(Skip = "Repository-local EHT geocentric file is a compatibility report, not authoritative survey-grade ground truth.")]
    public void OfficialGrid_EovToEtrs89_CompatibilityBenchmark_ReportOnly()
    {
        var points = LoadBenchmarkPoints();
        Assert.True(points.Count > 0, "Benchmark input is empty");

        var grs80 = new EllipsoidParameters(6378137.0, 6356752.31414, "GRS80");

        var xErrors = new List<double>();
        var yErrors = new List<double>();
        var zErrors = new List<double>();
        var rmsErrors = new List<double>();
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var etrs89 = _context.TransformHd72ToEtrs89(hd72);
                var geo = EllipsoidMath.EllipsoidToGeocentric(
                    EllipsoidMath.DegreesToRadians(etrs89.Latitude),
                    EllipsoidMath.DegreesToRadians(etrs89.Longitude),
                    etrs89.Height,
                    grs80);

                var dx = System.Math.Abs(geo.X - pt.ExpectedX);
                var dy = System.Math.Abs(geo.Y - pt.ExpectedY);
                var dz = System.Math.Abs(geo.Z - pt.ExpectedZ);

                xErrors.Add(dx);
                yErrors.Add(dy);
                zErrors.Add(dz);
                rmsErrors.Add(System.Math.Sqrt(dx * dx + dy * dy + dz * dz));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Official grid benchmark ({points.Count} points) ===");
        _output.WriteLine($"  covered: {covered}");
        Assert.True(covered > 0, "No benchmark points were covered");
        _output.WriteLine($"  X avg: {xErrors.Average():F4} m");
        _output.WriteLine($"  Y avg: {yErrors.Average():F4} m");
        _output.WriteLine($"  Z avg: {zErrors.Average():F4} m");
        _output.WriteLine($"  3D avg: {rmsErrors.Average():F4} m");
        _output.WriteLine($"  3D max: {rmsErrors.Max():F4} m");
        _output.WriteLine($"  3D p95: {Percentile(rmsErrors, 0.95):F4} m");
        _output.WriteLine($"  3D p99: {Percentile(rmsErrors, 0.99):F4} m");

        Assert.True(rmsErrors.Count > 0, "No benchmark errors were calculated");
    }

    [Fact]
    public void OfficialGrid_DigiterraBenchmark_ReportsAccuracy()
    {
        var points = LoadDigiterraPoints();
        Assert.True(points.Count > 0, "Digiterra benchmark input is empty");

        var errors = new List<double>(points.Count);
        var latErrors = new List<double>(points.Count);
        var lonErrors = new List<double>(points.Count);
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, 0);
                var wgs84 = _context.TransformHd72ToWgs84(hd72);

                latErrors.Add(System.Math.Abs(wgs84.Latitude - pt.ExpectedLat));
                lonErrors.Add(System.Math.Abs(wgs84.Longitude - pt.ExpectedLon));
                errors.Add(HaversineMeters(pt.ExpectedLat, pt.ExpectedLon, wgs84.Latitude, wgs84.Longitude));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Official grid benchmark: Digiterra ({points.Count} points) ===");
        _output.WriteLine($"  covered: {covered}");
        if (covered == 0)
            return;
        WriteDistanceStats(errors);
        _output.WriteLine($"  lat avg: {latErrors.Average():G17} deg");
        _output.WriteLine($"  lat max: {latErrors.Max():G17} deg");
        _output.WriteLine($"  lon avg: {lonErrors.Average():G17} deg");
        _output.WriteLine($"  lon max: {lonErrors.Max():G17} deg");
    }

    [Fact]
    public void OfficialGrid_Eht41Benchmark_ReportsAccuracy()
    {
        var points = LoadEht41Points();
        Assert.True(points.Count > 0, "EHT 4.1 benchmark input is empty");

        var horizontalErrors = new List<double>(points.Count);
        var latErrors = new List<double>(points.Count);
        var lonErrors = new List<double>(points.Count);
        var heightErrors = new List<double>(points.Count);
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
                var wgs84 = _context.TransformHd72ToWgs84(hd72);

                latErrors.Add(System.Math.Abs(wgs84.Latitude - pt.ExpectedLat));
                lonErrors.Add(System.Math.Abs(wgs84.Longitude - pt.ExpectedLon));
                heightErrors.Add(System.Math.Abs(wgs84.Height - pt.ExpectedH));
                horizontalErrors.Add(HaversineMeters(pt.ExpectedLat, pt.ExpectedLon, wgs84.Latitude, wgs84.Longitude));
                covered++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not cover"))
            {
                continue;
            }
        }

        _output.WriteLine($"=== Official grid benchmark: EHT 4.1 ({points.Count} points) ===");
        _output.WriteLine($"  covered: {covered}");
        if (covered == 0)
            return;
        WriteDistanceStats(horizontalErrors);
        _output.WriteLine($"  lat avg: {latErrors.Average():G17} deg");
        _output.WriteLine($"  lat max: {latErrors.Max():G17} deg");
        _output.WriteLine($"  lon avg: {lonErrors.Average():G17} deg");
        _output.WriteLine($"  lon max: {lonErrors.Max():G17} deg");
        _output.WriteLine($"  height avg: {heightErrors.Average():G17} m");
        _output.WriteLine($"  height max: {heightErrors.Max():G17} m");
    }

    [Fact]
    public async Task LegacyDefault_DigiterraBenchmark_ReportsAccuracy()
    {
        var points = LoadDigiterraPoints();
        Assert.True(points.Count > 0, "Digiterra benchmark input is empty");

        var transformer = (ILegacyCoordinateTransformer)TransformerFactory.CreateDefault();
        await EnsureReadyAsync(transformer);

        var errors = new List<double>(points.Count);

        foreach (var pt in points)
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, 0);
            var wgs84 = transformer.Transform(hd72);
            errors.Add(HaversineMeters(pt.ExpectedLat, pt.ExpectedLon, wgs84.Latitude, wgs84.Longitude));
        }

        _output.WriteLine($"=== Legacy default benchmark: Digiterra ({points.Count} points) ===");
        WriteDistanceStats(errors);
        Assert.True(errors.Count > 0, "No Digiterra points were transformed");
    }

    [Fact]
    public async Task LegacyDefault_Eht41Benchmark_ReportsAccuracy()
    {
        var points = LoadEht41Points();
        Assert.True(points.Count > 0, "EHT 4.1 benchmark input is empty");

        var transformer = (ILegacyCoordinateTransformer)TransformerFactory.CreateDefault();
        await EnsureReadyAsync(transformer);

        var errors = new List<double>(points.Count);

        foreach (var pt in points)
        {
            var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
            var wgs84 = transformer.Transform(hd72);
            errors.Add(HaversineMeters(pt.ExpectedLat, pt.ExpectedLon, wgs84.Latitude, wgs84.Longitude));
        }

        _output.WriteLine($"=== Legacy default benchmark: EHT 4.1 ({points.Count} points) ===");
        WriteDistanceStats(errors);
        Assert.True(errors.Count > 0, "No EHT 4.1 points were transformed");
    }

    private record BenchmarkPoint(double EovY, double EovX, double EovH, double ExpectedX, double ExpectedY, double ExpectedZ);
    private record DigiterraPoint(double EovY, double EovX, double ExpectedLon, double ExpectedLat);
    private record Eht41Point(double EovY, double EovX, double EovH, double ExpectedLat, double ExpectedLon, double ExpectedH);

    private static double ParseHu(string s) => TestHelpers.ParseHu(s);

    private static List<BenchmarkPoint> LoadBenchmarkPoints()
    {
        var inputPath = ResolveExistingPath("source", "HuGeo.Test", "TestData", "Benchmark", "eov-eht-input.txt");
        var outputPath = ResolveExistingPath("source", "HuGeo.Test", "TestData", "Benchmark", "eov-eht-output.txt");

        var inputLines = File.ReadAllLines(inputPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("PONT"))
            .ToArray();

        var outputLines = File.ReadAllLines(outputPath)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("PONT"))
            .ToArray();

        var count = System.Math.Min(inputLines.Length, outputLines.Length);
        var points = new List<BenchmarkPoint>(count);

        for (var i = 0; i < count; i++)
        {
            var inParts = inputLines[i].Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var outParts = outputLines[i].Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (inParts.Length < 4 || outParts.Length < 4)
                continue;

            points.Add(new BenchmarkPoint(
                ParseHu(inParts[1]),
                ParseHu(inParts[2]),
                ParseHu(inParts[3]),
                ParseHu(outParts[1]),
                ParseHu(outParts[2]),
                ParseHu(outParts[3])));
        }

        return points;
    }

    private static List<DigiterraPoint> LoadDigiterraPoints()
    {
        var path = ResolveExistingPath("source", "HuGeo.Test", "TestData", "Benchmark", "eov-wgs84-dt-test.txt");
        var points = new List<DigiterraPoint>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                continue;

            points.Add(new DigiterraPoint(
                ParseHu(parts[0]),
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3])));
        }

        return points;
    }

    private static List<Eht41Point> LoadEht41Points()
    {
        var path = ResolveExistingPath("source", "HuGeo.Test", "TestData", "Benchmark", "eov-wgs84-eht-test.txt");
        var points = new List<Eht41Point>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;

            points.Add(new Eht41Point(
                ParseHu(parts[0]),
                ParseHu(parts[1]),
                ParseHu(parts[2]),
                ParseHu(parts[3]),
                ParseHu(parts[4]),
                ParseHu(parts[5])));
        }

        return points;
    }

    private void WriteDistanceStats(List<double> errors)
    {
        _output.WriteLine($"  2D avg: {errors.Average():G17} m");
        _output.WriteLine($"  2D max: {errors.Max():G17} m");
        _output.WriteLine($"  2D p95: {Percentile(errors, 0.95):G17} m");
        _output.WriteLine($"  2D p99: {Percentile(errors, 0.99):G17} m");
    }

    private static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg) =>
        TestHelpers.HaversineMeters(lat1Deg, lon1Deg, lat2Deg, lon2Deg);

    private static async Task EnsureReadyAsync(ICoordinateTransformer transformer)
    {
        if (!transformer.IsReady && transformer is CoordinateTransformer concrete)
            await concrete.InitializeAsync();
    }

    private static string ResolveExistingPath(params string[] segments)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(segments)),
            Path.Combine(AppContext.BaseDirectory, Path.Combine("..", "..", "..", "..", "..", Path.Combine(segments))),
            Path.Combine(AppContext.BaseDirectory, Path.Combine("..", "..", "..", "..", "..", "..", Path.Combine(segments))),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        throw new FileNotFoundException($"Could not resolve benchmark file: {Path.Combine(segments)}");
    }

    private static double Percentile(List<double> values, double p) =>
        TestHelpers.Percentile(values, p);
}
