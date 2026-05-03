using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;
using Xunit.Abstractions;

namespace HuGeo.Tests.Transformations;

public class OfficialEhtWebReferenceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TransformationContext _context = null!;

    public OfficialEhtWebReferenceTests(ITestOutputHelper output) => _output = output;

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
    public void OfficialEovToEtrs89_WebFixtureMatchesService()
    {
        var points = LoadOfficialPoints();
        Assert.True(points.Count > 0, "Official fixture is empty");

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

        _output.WriteLine($"=== Official forward: EOV -> ETRS89 ===");
        _output.WriteLine($"  covered: {covered} / {points.Count}");
        if (covered > 0)
        {
            _output.WriteLine($"  lat avg: {latErrors.Average():G17} deg");
            _output.WriteLine($"  lat max: {latErrors.Max():G17} deg");
            _output.WriteLine($"  lon avg: {lonErrors.Average():G17} deg");
            _output.WriteLine($"  lon max: {lonErrors.Max():G17} deg");
            _output.WriteLine($"  height avg: {heightErrors.Average():G17} m");
            _output.WriteLine($"  height max: {heightErrors.Max():G17} m");
            _output.WriteLine($"  2D avg: {horizErrors.Average():G17} m");
            _output.WriteLine($"  2D max: {horizErrors.Max():G17} m");
            _output.WriteLine($"  2D p95: {Percentile(horizErrors, 0.95):G17} m");
            _output.WriteLine($"  2D p99: {Percentile(horizErrors, 0.99):G17} m");
        }

        Assert.True(covered >= points.Count * 0.95, $"Forward coverage too low: {covered}/{points.Count}");
        Assert.True(latErrors.Count > 0, "No forward points were evaluated");
        Assert.True(latErrors.Max() <= 2e-7, $"Forward latitude max error too large: {latErrors.Max():G17} deg");
        Assert.True(lonErrors.Max() <= 2e-7, $"Forward longitude max error too large: {lonErrors.Max():G17} deg");
        Assert.True(heightErrors.Max() <= 0.005, $"Forward height max error too large: {heightErrors.Max():G17} m");
        Assert.True(horizErrors.Max() <= 0.02, $"Forward horizontal max error too large: {horizErrors.Max():G17} m");
    }

    [Fact]
    public void OfficialEtrs89ToEov_WebFixtureRoundTripsToSource()
    {
        var points = LoadOfficialPoints();
        Assert.True(points.Count > 0, "Official fixture is empty");

        var yErrors = new List<double>();
        var xErrors = new List<double>();
        var hErrors = new List<double>();
        var planarErrors = new List<double>();
        var covered = 0;

        foreach (var pt in points)
        {
            try
            {
                var etrs89 = new Etrs89Coordinate(pt.ExpectedLat, pt.ExpectedLon, pt.ExpectedH);
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

        _output.WriteLine($"=== Official reverse: ETRS89 -> EOV (covered points) ===");
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
            _output.WriteLine($"  2D p95: {Percentile(planarErrors, 0.95):G17} m");
            _output.WriteLine($"  2D p99: {Percentile(planarErrors, 0.99):G17} m");
        }

        Assert.True(covered >= points.Count * 0.95, $"Reverse coverage too low: {covered}/{points.Count}");
        Assert.True(yErrors.Count > 0, "No reverse points were evaluated");
        Assert.True(yErrors.Max() <= 0.02, $"Reverse easting max error too large: {yErrors.Max():G17} m");
        Assert.True(xErrors.Max() <= 0.02, $"Reverse northing max error too large: {xErrors.Max():G17} m");
        Assert.True(hErrors.Max() <= 0.005, $"Reverse height max error too large: {hErrors.Max():G17} m");
        Assert.True(planarErrors.Max() <= 0.02, $"Reverse planar max error too large: {planarErrors.Max():G17} m");
    }

    [Fact]
    public void OfficialEtrs89ToEov_SeparateDatabaseMatchesService()
    {
        var points = LoadReverseOfficialPoints();
        Assert.True(points.Count > 0, "Reverse official fixture is empty");

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

        _output.WriteLine($"=== Official reverse DB: ETRS89 -> EOV ===");
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
            _output.WriteLine($"  2D p95: {Percentile(planarErrors, 0.95):G17} m");
            _output.WriteLine($"  2D p99: {Percentile(planarErrors, 0.99):G17} m");
        }

        Assert.True(covered >= points.Count * 0.95, $"Reverse DB coverage too low: {covered}/{points.Count}");
        Assert.True(yErrors.Count > 0, "No reverse DB points were evaluated");
        Assert.True(yErrors.Max() <= 0.02, $"Reverse DB easting max error too large: {yErrors.Max():G17} m");
        Assert.True(xErrors.Max() <= 0.02, $"Reverse DB northing max error too large: {xErrors.Max():G17} m");
        Assert.True(hErrors.Max() <= 0.005, $"Reverse DB height max error too large: {hErrors.Max():G17} m");
        Assert.True(planarErrors.Max() <= 0.02, $"Reverse DB planar max error too large: {planarErrors.Max():G17} m");
    }

    private record OfficialPoint(
        string PointNumber,
        double EovY,
        double EovX,
        double EovH,
        double ExpectedLat,
        double ExpectedLon,
        double ExpectedH);

    private record ReverseOfficialPoint(
        string PointNumber,
        double Latitude,
        double Longitude,
        double Height,
        double EovY,
        double EovX,
        double EovH);

    private static double ParseHu(string s) => TestHelpers.ParseHu(s);

    private static List<OfficialPoint> LoadOfficialPoints()
    {
        var assembly = typeof(OfficialEhtWebReferenceTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.eov-etrs89-official.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<OfficialPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new OfficialPoint(
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

    private static List<ReverseOfficialPoint> LoadReverseOfficialPoints()
    {
        var assembly = typeof(OfficialEhtWebReferenceTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.Official.etrs89-eov-official.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<ReverseOfficialPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7)
                continue;

            points.Add(new ReverseOfficialPoint(
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
