namespace HuGeo.Core.Math;

/// <summary>
/// TECA grid_delta.dat bináris rács: WGS84 lat/lon szerint indexelt EOV korrekciók.
/// Rácsparaméterek a TECA kódból: 50 sor × 116 oszlop, 0.06° lépés.
/// Bal felső sarok: lon=16.05°, lat=48.65°.
/// Bilineáris interpoláció (pontosan a TECA residual() függvényének C# portja).
/// </summary>
[Obsolete("Use the official grid pipeline for production. Keep this only for legacy TECA regression checks.")]
public class TecaBilinearGrid
{
    public const int Rows = 50;
    public const int Cols = 116;
    public const double Step = 0.06;        // fok
    public const double Lon0 = 16.05;       // bal szél (min lon)
    public const double Lat0 = 48.65;       // felső szél (max lat)

    private readonly double[] _dx;  // EOV X (Northing) korrekcók, [row*Cols + col]
    private readonly double[] _dy;  // EOV Y (Easting) korrekciók
    private readonly double[] _dz;  // Magasság-korrekciók

    public TecaBilinearGrid(double[] dx, double[] dy, double[] dz)
    {
        _dx = dx;
        _dy = dy;
        _dz = dz;
    }

    /// <summary>
    /// Bilineáris interpoláció WGS84 lat/lon koordinátákra.
    /// Visszaadja az (dx_eov, dy_eov, dh) EOV korrekciókat méterben.
    /// Null ha a pont Magyarországon kívül esik.
    /// </summary>
    public (double Dx, double Dy, double Dh)? Interpolate(double wgsLat, double wgsLon)
    {
        double u = (wgsLon - Lon0) / Step;   // oszlopindex (törtszám)
        double v = (Lat0 - wgsLat) / Step;   // sorindex (törtszám, felülről lefelé)

        // 4 szomszédos gridpont indexei — TECA ceil/floor logikája
        int i1 = (int)System.Math.Ceiling(u);    // jobb
        int i2 = (int)System.Math.Floor(u);      // bal
        int j1 = (int)System.Math.Floor(v);      // felső sor
        int j2 = (int)System.Math.Ceiling(v);    // alsó sor

        if (i1 < 0 || i1 >= Cols || i2 < 0 || i2 >= Cols || j1 < 0 || j1 >= Rows || j2 < 0 || j2 >= Rows)
            return null;

        // Gridpontok WGS84 koordinátái
        double x1 = Lon0 + i1 * Step;  // jobb oszlop lon
        double x2 = Lon0 + i2 * Step;  // bal oszlop lon
        double y1 = Lat0 - j1 * Step;  // felső sor lat
        double y2 = Lat0 - j2 * Step;  // alsó sor lat

        // Bilineáris súlyok = ellentétes sarokhoz húzott téglalap területe (TECA módszer)
        // p1=jobb-felső(x1,y1), p2=bal-felső(x2,y1), p3=bal-alsó(x2,y2), p4=jobb-alsó(x1,y2)
        // TECA: t1=p1 távolsága (→ p3 súlya), t2=p2 távolsága (→ p4 súlya)
        //        t3=p3 távolsága (→ p1 súlya), t4=p4 távolsága (→ p2 súlya)
        double t1 = System.Math.Abs((wgsLon - x1) * (wgsLat - y1));  // p1 területe → p3 súly
        double t2 = System.Math.Abs((wgsLon - x2) * (wgsLat - y1));  // p2 területe → p4 súly  (y1=felső!)
        double t3 = System.Math.Abs((wgsLon - x2) * (wgsLat - y2));  // p3 területe → p1 súly
        double t4 = System.Math.Abs((wgsLon - x1) * (wgsLat - y2));  // p4 területe → p2 súly  (y2=alsó!)

        // TECA indexek: p1=(i1,j1), p2=(i2,j1), p3=(i2,j2), p4=(i1,j2)
        // ahol i=oszlop(lon), j=sor(lat-csökkő irány)
        int idx1 = j1 * Cols + i1;   // jobb-felső
        int idx2 = j1 * Cols + i2;   // bal-felső
        int idx3 = j2 * Cols + i2;   // bal-alsó
        int idx4 = j2 * Cols + i1;   // jobb-alsó

        if (!ValidIndex(idx1) || !ValidIndex(idx2) || !ValidIndex(idx3) || !ValidIndex(idx4))
            return null;

        double d2 = Step * Step;

        // TECA képlet: (t3*p1 + t4*p2 + t1*p3 + t2*p4) / d²
        double dx = (t3 * _dx[idx1] + t4 * _dx[idx2] + t1 * _dx[idx3] + t2 * _dx[idx4]) / d2;
        double dy = (t3 * _dy[idx1] + t4 * _dy[idx2] + t1 * _dy[idx3] + t2 * _dy[idx4]) / d2;
        double dz = (t3 * _dz[idx1] + t4 * _dz[idx2] + t1 * _dz[idx3] + t2 * _dz[idx4]) / d2;

        return (dx, dy, dz);
    }

    private bool ValidIndex(int idx)
    {
        if (idx < 0 || idx >= _dx.Length) return false;
        // TECA -256 sentinel = Magyarországon kívüli cella
        return _dx[idx] > -255.0;
    }
}
