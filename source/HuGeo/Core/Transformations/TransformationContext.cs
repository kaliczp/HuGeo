using HuGeo.Core.Coordinates;
using HuGeo.Core.Ellipsoids;
using HuGeo.Core.Math;

namespace HuGeo.Core.Transformations;

public class TransformationContext
{
    private readonly HelmertTransformation _helmert = new();
    private readonly TransformationMode _mode;

    // Legacy HD72->WGS84 corrections to WGS84 geocentric (meters), keyed by EOV Y/X
    private readonly Func<double, double, (double dF, double dL, double dH)?>? _getHd72Corrections;

    // Legacy WGS84->HD72 corrections to EOV coordinates (meters), keyed by WGS84 lat/lon
    private readonly Func<double, double, (double dY, double dX, double dZ)?>? _getWgs84Corrections;

    // Official HD72/EOV -> ETRS89 horizontal grid, keyed by geodetic lat/lon in degrees.
    private readonly Func<double, double, (double dLatArcSec, double dLonArcSec)?>? _getOfficialCorrections;
    private readonly Func<double, double, double?>? _getOfficialHeightCorrection;

    public TransformationContext(
        TransformationMode mode = TransformationMode.GridWithFallback,
        Func<double, double, (double dF, double dL, double dH)?>? getHd72Corrections = null,
        Func<double, double, (double dY, double dX, double dZ)?>? getWgs84Corrections = null,
        Func<double, double, (double dLatArcSec, double dLonArcSec)?>? getOfficialCorrections = null,
        Func<double, double, double?>? getOfficialHeightCorrection = null)
    {
        _mode = mode;
        _getHd72Corrections = getHd72Corrections;
        _getWgs84Corrections = getWgs84Corrections;
        _getOfficialCorrections = getOfficialCorrections;
        _getOfficialHeightCorrection = getOfficialHeightCorrection;
    }

    /// <summary>
    /// HD72 (EOV) -> WGS84 pipeline.
    /// Legacy compatibility path. For survey workflows prefer HD72 -> ETRS89 -> EOV
    /// with the explicit ETRS89 API.
    /// </summary>
    public Wgs84Coordinate TransformHd72ToWgs84(Hd72Coordinate hd72)
    {
        var etrs89 = TransformHd72ToEtrs89(hd72);
        return TransformEtrs89ToWgs84(etrs89);
    }

    /// <summary>
    /// HD72 (EOV) -> ETRS89 pipeline.
    /// This is the explicit survey-grade branch when the official horizontal grid is enabled.
    /// </summary>
    public Etrs89Coordinate TransformHd72ToEtrs89(Hd72Coordinate hd72)
    {
        hd72.Validate();

        try
        {
            if (_mode == TransformationMode.OfficialGrid)
            {
                if (_getOfficialCorrections == null)
                    throw new InvalidOperationException("Official grid is not available.");
            }

            var (srcLatRad, srcLonRad, srcH) = GaussProjection.EovToGrs67(hd72.Easting, hd72.Northing, hd72.Height);
            var srcLatDeg = EllipsoidMath.RadiansToDegrees(srcLatRad);
            var srcLonDeg = EllipsoidMath.RadiansToDegrees(srcLonRad);

            if (_mode == TransformationMode.OfficialGrid && _getOfficialCorrections != null)
            {
                var corr = _getOfficialCorrections(srcLatDeg, srcLonDeg);
                if (corr != null)
                {
                    var h = srcH;
                    if (_getOfficialHeightCorrection == null)
                        throw new InvalidOperationException("Official geoid grid is not available.");

                    var geo = _getOfficialHeightCorrection(srcLatDeg, srcLonDeg);
                    if (geo == null)
                        throw new InvalidOperationException(
                            $"Official geoid grid does not cover HD72 point lat={srcLatDeg:F8}, lon={srcLonDeg:F8}.");

                    h = srcH + geo.Value;

                    return new Etrs89Coordinate(
                        srcLatDeg + corr.Value.dLatArcSec / 3600.0,
                        srcLonDeg + corr.Value.dLonArcSec / 3600.0,
                        h);
                }

                throw new InvalidOperationException(
                    $"Official grid does not cover HD72 point lat={srcLatDeg:F8}, lon={srcLonDeg:F8}.");
            }

            var (grsX, grsY, grsZ) = EllipsoidMath.EllipsoidToGeocentric(srcLatRad, srcLonRad, srcH, GRS67.Parameters);
            var (eurX, eurY, eurZ) = _helmert.TransformGrs67ToWgs84(grsX, grsY, grsZ);

            if (_mode != TransformationMode.HelmertOnly && _getHd72Corrections != null)
            {
                var corr = _getHd72Corrections(hd72.Easting, hd72.Northing);
                if (corr != null)
                {
                    eurX += corr.Value.dF;
                    eurY += corr.Value.dL;
                    eurZ += corr.Value.dH;
                }
            }

            var (wgsLat, wgsLon, wgsH) = EllipsoidMath.GeocentricToEllipsoid(eurX, eurY, eurZ, WGS84.Parameters);
            return new Etrs89Coordinate(
                EllipsoidMath.RadiansToDegrees(wgsLat),
                EllipsoidMath.RadiansToDegrees(wgsLon),
                wgsH);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException("HD72 to ETRS89 transformation failed", ex);
        }
    }

    /// <summary>
    /// ETRS89 -> HD72 (EOV) pipeline.
    /// This is the explicit survey-grade branch when the official horizontal grid is enabled.
    /// </summary>
    public Hd72Coordinate TransformEtrs89ToHd72(Etrs89Coordinate etrs89)
    {
        etrs89.Validate();

        try
        {
            if (_mode == TransformationMode.OfficialGrid)
            {
                if (_getOfficialCorrections == null)
                    throw new InvalidOperationException("Official grid is not available.");
            }

            if (_mode == TransformationMode.OfficialGrid && _getOfficialCorrections != null)
            {
                // The official horizontal grid is defined in the HD72/GRS67 geographic frame.
                // For the reverse direction we first sample at the ETRS89 coordinate, then
                // subtract the offset to obtain the HD72 latitude/longitude estimate. The
                // remaining error is bounded by the local grid-gradient over the datum shift.
                // The checked official fixtures currently hold this reverse approximation
                // within 2 cm horizontally for covered points.
                var corr = _getOfficialCorrections(etrs89.Latitude, etrs89.Longitude);
                if (corr != null)
                {
                    var srcLatDeg = etrs89.Latitude - corr.Value.dLatArcSec / 3600.0;
                    var srcLonDeg = etrs89.Longitude - corr.Value.dLonArcSec / 3600.0;
                    var officialHeight = etrs89.Height;
                    if (_getOfficialHeightCorrection == null)
                        throw new InvalidOperationException("Official geoid grid is not available.");

                    var geo = _getOfficialHeightCorrection(srcLatDeg, srcLonDeg);
                    if (geo == null)
                        throw new InvalidOperationException(
                            $"Official geoid grid does not cover HD72 estimate lat={srcLatDeg:F8}, lon={srcLonDeg:F8}.");

                    officialHeight = etrs89.Height - geo.Value;

                    var (officialEovY, officialEovX, _) = GaussProjection.Grs67ToEov(
                        EllipsoidMath.DegreesToRadians(srcLatDeg),
                        EllipsoidMath.DegreesToRadians(srcLonDeg),
                        officialHeight);

                    return new Hd72Coordinate(officialEovY, officialEovX, officialHeight);
                }

                throw new InvalidOperationException(
                    $"Official grid does not cover ETRS89 point lat={etrs89.Latitude:F8}, lon={etrs89.Longitude:F8}.");
            }

            var wgs84 = new Wgs84Coordinate(etrs89.Latitude, etrs89.Longitude, etrs89.Height);
            return TransformWgs84ToHd72(wgs84);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException("ETRS89 to HD72 transformation failed", ex);
        }
    }

    /// <summary>
    /// WGS84 -> ETRS89 for the Hungarian survey workflow.
    /// </summary>
    /// <remarks>
    /// This is intentionally a no-op coordinate-type conversion. The library assumes the
    /// incoming GNSS "WGS84" coordinate is already compatible with the ETRS89 realization
    /// used by the official Hungarian EHT/PROJ grid workflow. The centimeter-relevant
    /// transformation is the following ETRS89 <-> HD72/EOV grid/geoid step.
    ///
    /// A true epoch-dependent WGS84 realization -> ETRS89 transformation would require
    /// source WGS84 realization, observation epoch, target ETRS89 epoch, and a velocity
    /// model or time-dependent Helmert parameters. That model is deliberately not
    /// implemented here.
    /// </remarks>
    public Etrs89Coordinate TransformWgs84ToEtrs89(Wgs84Coordinate wgs84)
    {
        wgs84.Validate();
        return new Etrs89Coordinate(wgs84.Latitude, wgs84.Longitude, wgs84.Height);
    }

    /// <summary>
    /// ETRS89 -> WGS84 for the Hungarian survey workflow.
    /// </summary>
    /// <remarks>
    /// This is the reverse no-op type conversion of <see cref="TransformWgs84ToEtrs89"/>.
    /// It does not calculate epoch-dependent datum motion. Callers that need a specific
    /// WGS84 realization and epoch must apply that geodetic model outside this library.
    /// </remarks>
    public Wgs84Coordinate TransformEtrs89ToWgs84(Etrs89Coordinate etrs89)
    {
        etrs89.Validate();
        return new Wgs84Coordinate(etrs89.Latitude, etrs89.Longitude, etrs89.Height);
    }

    /// <summary>
    /// WGS84 -> HD72 (EOV) pipeline.
    /// Legacy compatibility path. For survey workflows prefer WGS84 -> ETRS89 -> EOV.
    /// </summary>
    public Hd72Coordinate TransformWgs84ToHd72(Wgs84Coordinate wgs84)
    {
        wgs84.Validate();

        try
        {
            if (_mode == TransformationMode.OfficialGrid && _getOfficialCorrections != null)
            {
                var etrs89 = TransformWgs84ToEtrs89(wgs84);
                return TransformEtrs89ToHd72(etrs89);
            }

            var wgsLat = EllipsoidMath.DegreesToRadians(wgs84.Latitude);
            var wgsLon = EllipsoidMath.DegreesToRadians(wgs84.Longitude);
            var (eurX, eurY, eurZ) = EllipsoidMath.EllipsoidToGeocentric(wgsLat, wgsLon, wgs84.Height, WGS84.Parameters);
            var (grsX, grsY, grsZ) = _helmert.TransformWgs84ToGrs67(eurX, eurY, eurZ);
            var (grs67Lat, grs67Lon, grs67H) = EllipsoidMath.GeocentricToEllipsoid(grsX, grsY, grsZ, GRS67.Parameters);
            var (eovY, eovX, hOut) = GaussProjection.Grs67ToEov(grs67Lat, grs67Lon, grs67H);

            if (_mode != TransformationMode.HelmertOnly && _getWgs84Corrections != null)
            {
                var corr = _getWgs84Corrections(wgs84.Latitude, wgs84.Longitude);
                if (corr != null)
                {
                    eovY += corr.Value.dY;
                    eovX += corr.Value.dX;
                    hOut += corr.Value.dZ;
                }
            }

            return new Hd72Coordinate(eovY, eovX, hOut);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException("WGS84 to HD72 transformation failed", ex);
        }
    }
}
