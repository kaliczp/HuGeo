using HuGeo.Core.Coordinates;
using HuGeo.Core.Ellipsoids;
using HuGeo.Core.Math;

namespace HuGeo.Core.Transformations;

/// <summary>
/// TECA (Brolly Gábor) algoritmus C# portja — cm szintű pontossággal.
/// WGS84 ↔ EOV/HD72 bidirectionálisan, iteratív grid-inverzzel.
/// </summary>
[Obsolete("Use the explicit legacy TECA path only for compatibility/regression checks. Prefer the official survey-grade API for production use.")]
public class TecaTransformationContext
{
    private readonly TecaHelmertTransformation _helmert = new();
    private readonly TecaBilinearGrid? _grid;

    public TecaTransformationContext(TecaBilinearGrid? grid = null)
    {
        _grid = grid;
    }

    /// <summary>
    /// EOV (HD72) → WGS84.
    /// Ha a rács elérhető: iteratív inverz (2-3 lépés → cm szintű pontosság).
    /// </summary>
    public Wgs84Coordinate TransformHd72ToWgs84(Hd72Coordinate hd72)
    {
        hd72.Validate();

        if (_grid == null)
            return TransformHd72ToWgs84Direct(hd72.Easting, hd72.Northing, hd72.Height);

        // Iteratív inverz: eltávolítjuk a grid-korrekciót EOV-ból, majd alkalmazuk az inverz Helmert-et
        double eovY = hd72.Easting;
        double eovX = hd72.Northing;
        double eovH = hd72.Height;

        Wgs84Coordinate wgs = TransformHd72ToWgs84Direct(eovY, eovX, eovH);

        bool converged = false;
        for (int iter = 0; iter < 5; iter++)
        {
            var corr = _grid.Interpolate(wgs.Latitude, wgs.Longitude);
            if (corr == null) break;

            // grid_delta.dat: dx=EOV_X-korrekcó, dy=EOV_Y-korrekcó
            double adjY = hd72.Easting  - corr.Value.Dy;
            double adjX = hd72.Northing - corr.Value.Dx;
            double adjH = hd72.Height   - corr.Value.Dh;

            var wgsNew = TransformHd72ToWgs84Direct(adjY, adjX, adjH);

            // Konvergencia: ~1e-9° ≈ 0.1 mm
            if (System.Math.Abs(wgsNew.Latitude  - wgs.Latitude)  < 1e-9 &&
                System.Math.Abs(wgsNew.Longitude - wgs.Longitude) < 1e-9)
            {
                wgs = wgsNew;
                converged = true;
                break;
            }
            wgs = wgsNew;
        }

        if (!converged)
        {
            var corr = _grid.Interpolate(wgs.Latitude, wgs.Longitude);
            if (corr != null)
            {
                double adjY = hd72.Easting  - corr.Value.Dy;
                double adjX = hd72.Northing - corr.Value.Dx;
                double adjH = hd72.Height   - corr.Value.Dh;
                var wgsCheck = TransformHd72ToWgs84Direct(adjY, adjX, adjH);
                var delta = System.Math.Max(
                    System.Math.Abs(wgsCheck.Latitude  - wgs.Latitude),
                    System.Math.Abs(wgsCheck.Longitude - wgs.Longitude));
                if (delta > 1e-7)
                    throw new InvalidOperationException("TECA iterative inverse did not converge after 5 iterations");
            }
        }

        return wgs;
    }

    /// <summary>
    /// WGS84 → EOV (HD72) — TECA irányban, grid-korrekcióval.
    /// </summary>
    public Hd72Coordinate TransformWgs84ToHd72(Wgs84Coordinate wgs84)
    {
        wgs84.Validate();

        // Alap Helmert-transzformáció (rács nélkül)
        var (eovY, eovX, eovH) = Wgs84ToEovDirect(wgs84.Latitude, wgs84.Longitude, wgs84.Height);

        if (_grid == null)
            return new Hd72Coordinate(eovY, eovX, eovH);

        // Grid-korrekcó hozzáadása (TECA módszer: WGS84 szerint indexelve)
        var corr = _grid.Interpolate(wgs84.Latitude, wgs84.Longitude);
        if (corr != null)
        {
            eovX += corr.Value.Dx;
            eovY += corr.Value.Dy;
            eovH += corr.Value.Dh;
        }

        return new Hd72Coordinate(eovY, eovX, eovH);
    }

    // Tiszta Helmert-inverz, rács nélkül: EOV → WGS84
    private Wgs84Coordinate TransformHd72ToWgs84Direct(double eovY, double eovX, double height)
    {
        var (grs67Lat, grs67Lon, grs67H) = GaussProjection.EovToGrs67(eovY, eovX, height);
        var (grsX, grsY, grsZ) = EllipsoidMath.EllipsoidToGeocentric(grs67Lat, grs67Lon, grs67H, GRS67.Parameters);
        var (wX, wY, wZ) = _helmert.TransformIugg67ToWgs84(grsX, grsY, grsZ);
        var (wLat, wLon, wH) = EllipsoidMath.GeocentricToEllipsoid(wX, wY, wZ, WGS84.Parameters);
        return new Wgs84Coordinate(
            EllipsoidMath.RadiansToDegrees(wLat),
            EllipsoidMath.RadiansToDegrees(wLon),
            wH);
    }

    // Tiszta Helmert-előre, rács nélkül: WGS84 → EOV
    private (double eovY, double eovX, double h) Wgs84ToEovDirect(double lat, double lon, double h)
    {
        var wLat = EllipsoidMath.DegreesToRadians(lat);
        var wLon = EllipsoidMath.DegreesToRadians(lon);
        var (wX, wY, wZ) = EllipsoidMath.EllipsoidToGeocentric(wLat, wLon, h, WGS84.Parameters);
        var (grsX, grsY, grsZ) = _helmert.TransformWgs84ToIugg67(wX, wY, wZ);
        var (grs67Lat, grs67Lon, grs67H) = EllipsoidMath.GeocentricToEllipsoid(grsX, grsY, grsZ, GRS67.Parameters);
        return GaussProjection.Grs67ToEov(grs67Lat, grs67Lon, grs67H);
    }
}
