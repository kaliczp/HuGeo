using static System.Math;

namespace HuGeo.Core.Math;

/// <summary>
/// Regular latitude/longitude grid of scalar height offsets in metres.
/// The grid is stored as top-left origin, row-major from north to south.
/// </summary>
public sealed class GeoidHeightGrid
{
    private const double NoDataValue = -32768.0;

    public int Rows { get; }
    public int Cols { get; }
    public double LonStep { get; }
    public double LatStep { get; }
    public double Lon0 { get; }
    public double Lat0 { get; }

    private readonly double[] _valuesMeters;

    public GeoidHeightGrid(
        int rows,
        int cols,
        double lonStep,
        double latStep,
        double lon0,
        double lat0,
        double[] valuesMeters)
    {
        if (rows <= 1 || cols <= 1)
            throw new ArgumentOutOfRangeException(nameof(rows), "Grid must have at least 2x2 points.");
        if (lonStep <= 0 || latStep <= 0)
            throw new ArgumentOutOfRangeException("Grid step must be positive.");
        if (valuesMeters.Length != rows * cols)
            throw new ArgumentException("Grid array size does not match rows/cols.");

        Rows = rows;
        Cols = cols;
        LonStep = lonStep;
        LatStep = latStep;
        Lon0 = lon0;
        Lat0 = lat0;
        _valuesMeters = valuesMeters;
    }

    public double? Interpolate(double latitudeDeg, double longitudeDeg)
    {
        return TryInterpolate(latitudeDeg, longitudeDeg, out var heightMeters)
            ? heightMeters
            : null;
    }

    public bool TryInterpolate(double latitudeDeg, double longitudeDeg, out double heightMeters)
    {
        var minLon = Lon0;
        var maxLon = Lon0 + LonStep * (Cols - 1);
        var maxLat = Lat0;
        var minLat = Lat0 - LatStep * (Rows - 1);

        if (longitudeDeg < minLon || longitudeDeg > maxLon || latitudeDeg < minLat || latitudeDeg > maxLat)
        {
            heightMeters = 0;
            return false;
        }

        var u = (longitudeDeg - Lon0) / LonStep;
        var v = (Lat0 - latitudeDeg) / LatStep;

        var i = (int)Floor(u);
        var j = (int)Floor(v);
        if (i < 0 || j < 0 || i > Cols - 2 || j > Rows - 2)
        {
            heightMeters = 0;
            return false;
        }

        var tx = u - i;
        var ty = v - j;

        var idx00 = j * Cols + i;
        var idx10 = j * Cols + (i + 1);
        var idx01 = (j + 1) * Cols + i;
        var idx11 = (j + 1) * Cols + (i + 1);

        var q00 = _valuesMeters[idx00];
        var q10 = _valuesMeters[idx10];
        var q01 = _valuesMeters[idx01];
        var q11 = _valuesMeters[idx11];

        if (IsNoData(q00) || IsNoData(q10) || IsNoData(q01) || IsNoData(q11))
        {
            heightMeters = 0;
            return false;
        }

        heightMeters = GridMath.Bilinear(q00, q10, q01, q11, tx, ty);
        return true;
    }

    private static bool IsNoData(double value) => value.Equals(NoDataValue) || double.IsNaN(value);
}
