using System.Globalization;

namespace HuGeo.Tests.Transformations;

internal static class TestHelpers
{
    public static double ParseHu(string s) =>
        double.Parse(s.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);

    public static double HaversineMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
    {
        const double earthRadiusMeters = 6371008.8;
        var lat1 = lat1Deg * System.Math.PI / 180.0;
        var lon1 = lon1Deg * System.Math.PI / 180.0;
        var lat2 = lat2Deg * System.Math.PI / 180.0;
        var lon2 = lon2Deg * System.Math.PI / 180.0;

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
                System.Math.Cos(lat1) * System.Math.Cos(lat2) *
                System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
        var c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    public static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var idx = (int)System.Math.Round((sorted.Count - 1) * p, MidpointRounding.AwayFromZero);
        idx = System.Math.Clamp(idx, 0, sorted.Count - 1);
        return sorted[idx];
    }
}

internal static class EhtTestData
{
    internal record EhtTestPoint(
        double EovY, double EovX, double EovH,
        double ExpectedLat, double ExpectedLon, double ExpectedH);

    public static List<EhtTestPoint> LoadEhtPoints()
    {
        var assembly = typeof(EhtRegressionTests).Assembly;
        var resourceName = "HuGeo.Tests.TestData.eov-wgs84-eht-test.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var points = new List<EhtTestPoint>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) continue;

            try
            {
                points.Add(new EhtTestPoint(
                    EovY: TestHelpers.ParseHu(parts[0]),
                    EovX: TestHelpers.ParseHu(parts[1]),
                    EovH: TestHelpers.ParseHu(parts[2]),
                    ExpectedLat: TestHelpers.ParseHu(parts[3]),
                    ExpectedLon: TestHelpers.ParseHu(parts[4]),
                    ExpectedH: TestHelpers.ParseHu(parts[5])));
            }
            catch { }
        }
        return points;
    }
}

internal static class OfficialFixtureData
{
    internal record ForwardPoint(
        string PointNumber,
        double EovY,
        double EovX,
        double EovH,
        double ExpectedLat,
        double ExpectedLon,
        double ExpectedH);

    internal record ReversePoint(
        string PointNumber,
        double Latitude,
        double Longitude,
        double Height,
        double EovY,
        double EovX,
        double EovH);

    private const string ForwardOfficialResource = "HuGeo.Tests.TestData.Official.eov-etrs89-official.txt";
    private const string ReverseOfficialResource = "HuGeo.Tests.TestData.Official.etrs89-eov-official.txt";
    private const string ForwardExtendedResource = "HuGeo.Tests.TestData.Official.eov-etrs89-official-extended.txt";
    private const string ReverseExtendedResource = "HuGeo.Tests.TestData.Official.etrs89-eov-official-extended.txt";

    public static List<ForwardPoint> LoadOfficialForwardPoints() =>
        LoadForwardPoints(ForwardOfficialResource);

    public static List<ReversePoint> LoadOfficialReversePoints() =>
        LoadReversePoints(ReverseOfficialResource);

    public static List<ForwardPoint> LoadExtendedForwardPoints() =>
        LoadForwardPoints(ForwardExtendedResource);

    public static List<ReversePoint> LoadExtendedReversePoints() =>
        LoadReversePoints(ReverseExtendedResource);

    private static List<ForwardPoint> LoadForwardPoints(string resourceName)
    {
        using var reader = OpenResourceReader(resourceName);

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
                parts[0].Trim(),
                TestHelpers.ParseHu(parts[1]),
                TestHelpers.ParseHu(parts[2]),
                TestHelpers.ParseHu(parts[3]),
                TestHelpers.ParseHu(parts[4]),
                TestHelpers.ParseHu(parts[5]),
                TestHelpers.ParseHu(parts[6])));
        }

        return points;
    }

    private static List<ReversePoint> LoadReversePoints(string resourceName)
    {
        using var reader = OpenResourceReader(resourceName);

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
                parts[0].Trim(),
                TestHelpers.ParseHu(parts[1]),
                TestHelpers.ParseHu(parts[2]),
                TestHelpers.ParseHu(parts[3]),
                TestHelpers.ParseHu(parts[4]),
                TestHelpers.ParseHu(parts[5]),
                TestHelpers.ParseHu(parts[6])));
        }

        return points;
    }

    private static StreamReader OpenResourceReader(string resourceName)
    {
        var assembly = typeof(OfficialFixtureData).Assembly;
        var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        return new StreamReader(stream);
    }
}
