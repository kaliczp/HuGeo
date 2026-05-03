using System.Globalization;
using HuGeo.DataAccess.Models;

namespace HuGeo.DataAccess.Loaders;

public class EmbeddedResourceGridLoader : IGridDataLoader
{
    // eov-wgs84.txt columns: y(Easting), x(Northing), df, dl, dh  (space or tab separated, dot decimal)
    // wgs84-eov.txt columns: fi(lat), la(lon), dy, dx, dz

    public Task<List<Hd72GridPoint>> LoadHd72GridAsync()
    {
        return Task.Run(() =>
        {
            var assembly = HuGeo.Resources.EmbeddedResources.Assembly;
            var resourceName = "HuGeo.Resources.Resources.eov-wgs84.txt";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Grid resource not found: {resourceName}");
            return ParseHd72Grid(stream);
        });
    }

    public Task<List<Wgs84GridPoint>> LoadWgs84GridAsync()
    {
        return Task.Run(() =>
        {
            var assembly = HuGeo.Resources.EmbeddedResources.Assembly;
            var resourceName = "HuGeo.Resources.Resources.wgs84-eov.txt";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Grid resource not found: {resourceName}");
            return ParseWgs84Grid(stream);
        });
    }

    public string GetLoadSource() => "Embedded Resources (HuGeo.Resources)";

    private static List<Hd72GridPoint> ParseHd72Grid(Stream stream)
    {
        var points = new List<Hd72GridPoint>();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            try
            {
                // col 0 = EOV Y (Easting), col 1 = EOV X (Northing)
                var eovY = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var eovX = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var dF = double.Parse(parts[2], CultureInfo.InvariantCulture);
                var dL = double.Parse(parts[3], CultureInfo.InvariantCulture);
                var dH = double.Parse(parts[4], CultureInfo.InvariantCulture);
                points.Add(new Hd72GridPoint(eovY, eovX, dF, dL, dH));
            }
            catch (FormatException) { }
        }
        return points;
    }

    private static List<Wgs84GridPoint> ParseWgs84Grid(Stream stream)
    {
        var points = new List<Wgs84GridPoint>();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            try
            {
                var fi = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var la = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var dY = double.Parse(parts[2], CultureInfo.InvariantCulture);
                var dX = double.Parse(parts[3], CultureInfo.InvariantCulture);
                var dZ = double.Parse(parts[4], CultureInfo.InvariantCulture);
                points.Add(new Wgs84GridPoint(fi, la, dY, dX, dZ));
            }
            catch (FormatException) { }
        }
        return points;
    }
}
