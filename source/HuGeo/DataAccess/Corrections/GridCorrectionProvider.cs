using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Models;
using HuGeo.Core.Math;

namespace HuGeo.DataAccess.Corrections;

public class GridCorrectionProvider
{
    private BilinearGrid? _hd72Grid;
    private BilinearGrid? _wgs84Grid;
    private GeodeticOffsetGrid? _officialGrid;
    private GeoidHeightGrid? _officialGeoidGrid;

    public Task InitializeAsync(
        IEnumerable<Hd72GridPoint> hd72Points,
        IEnumerable<Wgs84GridPoint> wgs84Points,
        GeodeticOffsetGrid? officialGrid = null,
        GeoidHeightGrid? officialGeoidGrid = null)
    {
        _hd72Grid = BilinearGrid.Create(
            hd72Points.Select(p => (p.Easting, p.Northing, p.DeltaLatitude, p.DeltaLongitude, p.DeltaHeight)));

        _wgs84Grid = BilinearGrid.Create(
            wgs84Points.Select(p => (p.Latitude, p.Longitude, p.DeltaEasting, p.DeltaNorthing, p.DeltaHeight)));

        _officialGrid = officialGrid;
        _officialGeoidGrid = officialGeoidGrid;

        return Task.CompletedTask;
    }

    /// <summary>
    /// EOV (HD72) bemeneti pontra illesztett WGS84 korrekció:
    /// delta latitude, delta longitude, delta height.
    /// </summary>
    public (double DeltaF, double DeltaL, double DeltaH)? GetHd72Corrections(double easting, double northing)
    {
        return _hd72Grid?.Interpolate(easting, northing);
    }

    /// <summary>
    /// WGS84 bemeneti pontra illesztett EOV korrekció:
    /// delta easting, delta northing, delta height.
    /// </summary>
    public (double DeltaY, double DeltaX, double DeltaZ)? GetWgs84Corrections(double latitude, double longitude)
    {
        return _wgs84Grid?.Interpolate(latitude, longitude);
    }

    /// <summary>
    /// Official HD72 -> ETRS89 correction grid in arc-seconds.
    /// DeltaLat and DeltaLon are positive in the grid's native direction.
    /// </summary>
    public (double DeltaLatArcSec, double DeltaLonArcSec)? GetOfficialCorrections(double latitude, double longitude)
    {
        return _officialGrid?.Interpolate(latitude, longitude);
    }

    public double? GetOfficialHeightCorrection(double latitude, double longitude)
    {
        return _officialGeoidGrid?.Interpolate(latitude, longitude);
    }

    public bool TryGetOfficialCorrections(double latitude, double longitude, out double deltaLatArcSec, out double deltaLonArcSec)
    {
        if (_officialGrid != null)
            return _officialGrid.TryInterpolate(latitude, longitude, out deltaLatArcSec, out deltaLonArcSec);

        deltaLatArcSec = 0;
        deltaLonArcSec = 0;
        return false;
    }

    public bool TryGetOfficialHeightCorrection(double latitude, double longitude, out double heightCorrection)
    {
        if (_officialGeoidGrid != null)
            return _officialGeoidGrid.TryInterpolate(latitude, longitude, out heightCorrection);

        heightCorrection = 0;
        return false;
    }

    public bool IsInitialized => _hd72Grid != null && _wgs84Grid != null;

    private sealed class BilinearGrid
    {
        private readonly double[] _xs;
        private readonly double[] _ys;
        private readonly Dictionary<(double X, double Y), (double V1, double V2, double V3)> _values;

        private BilinearGrid(
            double[] xs,
            double[] ys,
            Dictionary<(double X, double Y), (double V1, double V2, double V3)> values)
        {
            _xs = xs;
            _ys = ys;
            _values = values;
        }

        public static BilinearGrid Create(IEnumerable<(double X, double Y, double V1, double V2, double V3)> points)
        {
            var list = points.ToList();
            if (list.Count == 0)
                throw new InvalidOperationException("Grid data is empty");

            var xs = list.Select(p => p.X).Distinct().OrderBy(v => v).ToArray();
            var ys = list.Select(p => p.Y).Distinct().OrderBy(v => v).ToArray();

            var values = list.ToDictionary(p => (p.X, p.Y), p => (p.V1, p.V2, p.V3));
            return new BilinearGrid(xs, ys, values);
        }

        public (double V1, double V2, double V3)? Interpolate(double x, double y)
        {
            var xi = FindCellIndex(_xs, x);
            var yi = FindCellIndex(_ys, y);
            if (xi < 0 || yi < 0)
                return null;

            var x0 = _xs[xi];
            var x1 = _xs[xi + 1];
            var y0 = _ys[yi];
            var y1 = _ys[yi + 1];

            var tx = x1 == x0 ? 0.0 : (x - x0) / (x1 - x0);
            var ty = y1 == y0 ? 0.0 : (y - y0) / (y1 - y0);

            if (_values.TryGetValue((x0, y0), out var q00) &&
                _values.TryGetValue((x1, y0), out var q10) &&
                _values.TryGetValue((x0, y1), out var q01) &&
                _values.TryGetValue((x1, y1), out var q11))
            {
                return (
                    Bilinear(q00.V1, q10.V1, q01.V1, q11.V1, tx, ty),
                    Bilinear(q00.V2, q10.V2, q01.V2, q11.V2, tx, ty),
                    Bilinear(q00.V3, q10.V3, q01.V3, q11.V3, tx, ty));
            }

            return FallbackIdw(x, y);
        }

        private static int FindCellIndex(double[] axis, double value)
        {
            if (axis.Length < 2 || value < axis[0] || value > axis[^1])
                return -1;

            var idx = Array.BinarySearch(axis, value);
            if (idx >= 0)
            {
                if (idx == axis.Length - 1)
                    return idx - 1;
                return idx;
            }

            idx = ~idx - 1;
            if (idx < 0 || idx >= axis.Length - 1)
                return -1;

            return idx;
        }

        private static double Bilinear(double q00, double q10, double q01, double q11, double tx, double ty)
        {
            var r0 = q00 * (1 - tx) + q10 * tx;
            var r1 = q01 * (1 - tx) + q11 * tx;
            return r0 * (1 - ty) + r1 * ty;
        }

        private (double V1, double V2, double V3)? FallbackIdw(double x, double y)
        {
            var nearby = _values
                .Where(kv => System.Math.Abs(kv.Key.X - x) <= 2 * CellSize(_xs, x) &&
                             System.Math.Abs(kv.Key.Y - y) <= 2 * CellSize(_ys, y))
                .ToList();

            if (nearby.Count == 0)
                return null;

            if (nearby.Count == 1)
            {
                var only = nearby[0].Value;
                return only;
            }

            double sumW = 0, s1 = 0, s2 = 0, s3 = 0;
            foreach (var kv in nearby)
            {
                var dx = kv.Key.X - x;
                var dy = kv.Key.Y - y;
                var dist2 = dx * dx + dy * dy;
                if (dist2 < 1e-12)
                    return kv.Value;

                var w = 1.0 / dist2;
                sumW += w;
                s1 += kv.Value.V1 * w;
                s2 += kv.Value.V2 * w;
                s3 += kv.Value.V3 * w;
            }

            return (s1 / sumW, s2 / sumW, s3 / sumW);
        }

        private static double CellSize(double[] axis, double value)
        {
            if (axis.Length < 2)
                return 0;
            var idx = Array.BinarySearch(axis, value);
            if (idx >= 0 && idx < axis.Length - 1)
                return axis[idx + 1] - axis[idx];
            if (idx > 0 && idx < axis.Length)
                return axis[Math.Min(idx, axis.Length - 1)] - axis[idx - 1];
            return axis[1] - axis[0];
        }
    }
}
