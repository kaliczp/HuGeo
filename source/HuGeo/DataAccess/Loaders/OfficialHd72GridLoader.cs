using System.Globalization;
using HuGeo.Core.Math;

namespace HuGeo.DataAccess.Loaders;

public class OfficialHd72GridLoader
{
    public GeodeticOffsetGrid Load()
    {
        var assembly = HuGeo.Resources.EmbeddedResources.Assembly;
        using var binaryStream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.hu_bme_hd72corr.hgbin");
        if (binaryStream != null)
            return OfficialBinaryGridReader.ReadHd72(binaryStream);

        using var stream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.hu_bme_hd72corr.csv")
            ?? throw new InvalidOperationException("hu_bme_hd72corr.hgbin embedded resource not found");

        using var reader = new StreamReader(stream);
        var records = new List<(double Lat, double Lon, double DLat, double DLon)>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                continue;

            try
            {
                records.Add((
                    Parse(parts[0]),
                    Parse(parts[1]),
                    Parse(parts[2]),
                    Parse(parts[3])));
            }
            catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
            {
                // ignore malformed lines
            }
        }

        if (records.Count == 0)
            throw new InvalidOperationException("Official HD72 grid is empty");

        var latValues = records.Select(r => r.Lat).Distinct().OrderByDescending(v => v).ToArray();
        var lonValues = records.Select(r => r.Lon).Distinct().OrderBy(v => v).ToArray();

        if (latValues.Length * lonValues.Length != records.Count)
            throw new InvalidOperationException($"Official grid is not rectangular: lat={latValues.Length}, lon={lonValues.Length}, count={records.Count}");

        double stepLon = lonValues.Length > 1 ? lonValues[1] - lonValues[0] : 0.0;
        double stepLat = latValues.Length > 1 ? latValues[0] - latValues[1] : 0.0;
        var latOffsets = new double[records.Count];
        var lonOffsets = new double[records.Count];
        var indexByPoint = records
            .Select(r => (r.Lat, r.Lon, r.DLat, r.DLon))
            .ToDictionary(r => (r.Lat, r.Lon), r => (r.DLat, r.DLon));

        for (int j = 0; j < latValues.Length; j++)
        {
            for (int i = 0; i < lonValues.Length; i++)
            {
                if (!indexByPoint.TryGetValue((latValues[j], lonValues[i]), out var v))
                    throw new InvalidOperationException($"Missing official grid point at lat={latValues[j]}, lon={lonValues[i]}");

                var idx = j * lonValues.Length + i;
                latOffsets[idx] = v.DLat;
                lonOffsets[idx] = v.DLon;
            }
        }

        return new GeodeticOffsetGrid(
            rows: latValues.Length,
            cols: lonValues.Length,
            stepLon: stepLon,
            stepLat: stepLat,
            lon0: lonValues[0],
            lat0: latValues[0],
            latOffsetsArcsec: latOffsets,
            lonOffsetsArcsec: lonOffsets);
    }

    private static double Parse(string s) =>
        double.Parse(s.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
}
