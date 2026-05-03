using static System.Math;

namespace HuGeo.Core.Math;

/// <summary>
/// Regular latitude/longitude grid of horizontal offsets in arc-seconds.
/// The grid is stored as top-left origin, row-major from north to south.
/// </summary>
public sealed class GeodeticOffsetGrid
{
    public int Rows { get; }
    public int Cols { get; }
    public double Step { get; }
    public double StepLon { get; }
    public double StepLat { get; }
    public double Lon0 { get; }
    public double Lat0 { get; }

    private readonly double[] _latOffsetsArcsec;
    private readonly double[] _lonOffsetsArcsec;

    public GeodeticOffsetGrid(
        int rows,
        int cols,
        double step,
        double lon0,
        double lat0,
        double[] latOffsetsArcsec,
        double[] lonOffsetsArcsec)
        : this(rows, cols, step, step, lon0, lat0, latOffsetsArcsec, lonOffsetsArcsec)
    {
    }

    public GeodeticOffsetGrid(
        int rows,
        int cols,
        double stepLon,
        double stepLat,
        double lon0,
        double lat0,
        double[] latOffsetsArcsec,
        double[] lonOffsetsArcsec)
    {
        if (rows <= 1 || cols <= 1)
            throw new ArgumentOutOfRangeException(nameof(rows), "Grid must have at least 2x2 points.");
        if (stepLon <= 0 || stepLat <= 0)
            throw new ArgumentOutOfRangeException(nameof(stepLon), "Grid steps must be positive.");
        if (latOffsetsArcsec.Length != rows * cols || lonOffsetsArcsec.Length != rows * cols)
            throw new ArgumentException("Grid array sizes do not match rows/cols.");

        Rows = rows;
        Cols = cols;
        Step = stepLon;
        StepLon = stepLon;
        StepLat = stepLat;
        Lon0 = lon0;
        Lat0 = lat0;
        _latOffsetsArcsec = latOffsetsArcsec;
        _lonOffsetsArcsec = lonOffsetsArcsec;
    }

    public (double DeltaLatArcSec, double DeltaLonArcSec)? Interpolate(double latitudeDeg, double longitudeDeg)
    {
        return TryInterpolate(latitudeDeg, longitudeDeg, out var deltaLatArcSec, out var deltaLonArcSec)
            ? (deltaLatArcSec, deltaLonArcSec)
            : null;
    }

    public bool TryInterpolate(double latitudeDeg, double longitudeDeg, out double deltaLatArcSec, out double deltaLonArcSec)
    {
        var minLon = Lon0;
        var maxLon = Lon0 + StepLon * (Cols - 1);
        var maxLat = Lat0;
        var minLat = Lat0 - StepLat * (Rows - 1);

        if (longitudeDeg < minLon || longitudeDeg > maxLon || latitudeDeg < minLat || latitudeDeg > maxLat)
        {
            deltaLatArcSec = 0;
            deltaLonArcSec = 0;
            return false;
        }

        double u = (longitudeDeg - Lon0) / StepLon;
        double v = (Lat0 - latitudeDeg) / StepLat;

        int i = (int)Floor(u);
        int j = (int)Floor(v);
        if (i < 0 || j < 0 || i > Cols - 2 || j > Rows - 2)
        {
            deltaLatArcSec = 0;
            deltaLonArcSec = 0;
            return false;
        }

        double tx = u - i;
        double ty = v - j;

        int idx00 = j * Cols + i;
        int idx10 = j * Cols + (i + 1);
        int idx01 = (j + 1) * Cols + i;
        int idx11 = (j + 1) * Cols + (i + 1);

        deltaLatArcSec = GridMath.Bilinear(
            _latOffsetsArcsec[idx00],
            _latOffsetsArcsec[idx10],
            _latOffsetsArcsec[idx01],
            _latOffsetsArcsec[idx11],
            tx, ty);

        deltaLonArcSec = GridMath.Bilinear(
            _lonOffsetsArcsec[idx00],
            _lonOffsetsArcsec[idx10],
            _lonOffsetsArcsec[idx01],
            _lonOffsetsArcsec[idx11],
            tx, ty);

        return true;
    }
}
