using System.Text.Json;

namespace HuGeo.Core.Coordinates;

internal static class HungaryBoundary
{
    private static readonly Lazy<PolygonData> Polygon = new(LoadPolygon);

    public static bool Contains(double latitude, double longitude) =>
        Polygon.Value.Contains(longitude, latitude);

    private static PolygonData LoadPolygon()
    {
        var assembly = Resources.EmbeddedResources.Assembly;
        using var stream = assembly.GetManifestResourceStream("HuGeo.Resources.Resources.hungary.geojson")
            ?? throw new InvalidOperationException("Hungary boundary resource not found.");
        using var document = JsonDocument.Parse(stream);

        var geometry = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry");

        var type = geometry.GetProperty("type").GetString();
        var polygons = new List<RingSet>();

        if (string.Equals(type, "Polygon", StringComparison.Ordinal))
        {
            polygons.Add(ReadRingSet(geometry.GetProperty("coordinates")));
        }
        else if (string.Equals(type, "MultiPolygon", StringComparison.Ordinal))
        {
            foreach (var polygonElement in geometry.GetProperty("coordinates").EnumerateArray())
                polygons.Add(ReadRingSet(polygonElement));
        }
        else
        {
            throw new InvalidOperationException($"Unsupported Hungary boundary geometry type: {type}");
        }

        return new PolygonData(polygons);
    }

    private static RingSet ReadRingSet(JsonElement polygonElement)
    {
        var rings = polygonElement.EnumerateArray().ToArray();
        var shell = ReadRing(rings[0]);
        var holes = new List<double[][]>();
        for (var i = 1; i < rings.Length; i++)
            holes.Add(ReadRing(rings[i]));

        return new RingSet(shell, holes);
    }

    private static double[][] ReadRing(JsonElement ringElement)
    {
        var points = new List<double[]>();
        foreach (var pointElement in ringElement.EnumerateArray())
        {
            points.Add(new[]
            {
                pointElement[0].GetDouble(),
                pointElement[1].GetDouble(),
            });
        }

        return points.ToArray();
    }

    private sealed record PolygonData(IReadOnlyList<RingSet> Polygons)
    {
        public bool Contains(double longitude, double latitude)
        {
            foreach (var polygon in Polygons)
            {
                if (!PointInRing(polygon.Shell, longitude, latitude))
                    continue;

                var insideHole = false;
                foreach (var hole in polygon.Holes)
                {
                    if (PointInRing(hole, longitude, latitude))
                    {
                        insideHole = true;
                        break;
                    }
                }

                if (!insideHole)
                    return true;
            }

            return false;
        }
    }

    private sealed record RingSet(double[][] Shell, IReadOnlyList<double[][]> Holes);

    private static bool PointInRing(double[][] ring, double longitude, double latitude)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var xi = ring[i][0];
            var yi = ring[i][1];
            var xj = ring[j][0];
            var yj = ring[j][1];

            var intersects = ((yi > latitude) != (yj > latitude)) &&
                             (longitude < (xj - xi) * (latitude - yi) / ((yj - yi) + double.Epsilon) + xi);
            if (intersects)
                inside = !inside;
        }

        return inside;
    }
}
