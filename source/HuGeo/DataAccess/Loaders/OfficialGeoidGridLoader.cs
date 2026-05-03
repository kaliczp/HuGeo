using System.Globalization;
using HuGeo.Core.Math;

namespace HuGeo.DataAccess.Loaders;

public class OfficialGeoidGridLoader
{
    public GeoidHeightGrid Load()
    {
        var assembly = HuGeo.Resources.EmbeddedResources.Assembly;
        using var binaryStream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.hu_bme_geoid2014.hgbin");
        if (binaryStream != null)
            return OfficialBinaryGridReader.ReadGeoid(binaryStream);

        using var stream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.hu_bme_geoid2014.csv")
            ?? throw new InvalidOperationException("hu_bme_geoid2014.hgbin embedded resource not found");

        using var reader = new StreamReader(stream);
        var records = new List<(double Lat, double Lon, double Height)>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            var parts = trimmed.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            try
            {
                records.Add((
                    Parse(parts[0]),
                    Parse(parts[1]),
                    Parse(parts[2])));
            }
            catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
            {
                // ignore malformed lines
            }
        }

        if (records.Count == 0)
            throw new InvalidOperationException("Official geoid grid is empty");

        var latValues = records.Select(r => r.Lat).Distinct().OrderByDescending(v => v).ToArray();
        var lonValues = records.Select(r => r.Lon).Distinct().OrderBy(v => v).ToArray();

        if (latValues.Length * lonValues.Length != records.Count)
            throw new InvalidOperationException(
                $"Official geoid grid is not rectangular: lat={latValues.Length}, lon={lonValues.Length}, count={records.Count}");

        var latStep = latValues.Length > 1 ? latValues[0] - latValues[1] : 0.0;
        var lonStep = lonValues.Length > 1 ? lonValues[1] - lonValues[0] : 0.0;

        var values = new double[records.Count];
        var indexByPoint = records.ToDictionary(r => (r.Lat, r.Lon), r => r.Height);

        for (int j = 0; j < latValues.Length; j++)
        {
            for (int i = 0; i < lonValues.Length; i++)
            {
                if (!indexByPoint.TryGetValue((latValues[j], lonValues[i]), out var v))
                    throw new InvalidOperationException($"Missing official geoid point at lat={latValues[j]}, lon={lonValues[i]}");

                values[j * lonValues.Length + i] = v;
            }
        }

        return new GeoidHeightGrid(
            rows: latValues.Length,
            cols: lonValues.Length,
            lonStep: lonStep,
            latStep: latStep,
            lon0: lonValues[0],
            lat0: latValues[0],
            valuesMeters: values);
    }

    private static double Parse(string s) =>
        double.Parse(s.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
}
