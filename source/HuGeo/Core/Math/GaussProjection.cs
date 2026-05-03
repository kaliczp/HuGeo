using static System.Math;

namespace HuGeo.Core.Math
{
    // EOV convention: Y = Easting (E-W), X = Northing (N-S)
    public static class GaussProjection
    {
        private const double GaussRadius = 6379743.001;
        private const double F0 = 0.82205007768932923073105835195814;
        private const double L0 = 0.3324602953246920;
        private const double M0 = 0.99993;
        private const double K2 = 1.000719704936;
        private const double Av = 1.00155641;
        private const double Bv = 0.000000024436;
        private const double Cv = 6.5e-15;
        private const double Ro = 206264.806247;
        private const double Nfn = 0.823213630523992;
        private const double Kfn = 0.822438208856524;
        private const double An = 0.99844601;
        private const double Bn = 0.000000024323;
        private const double Cn = 5.3e-15;

        private const double EovYOrigin = 650000;
        private const double EovXOrigin = 200000;

        /// <summary>
        /// EOV projected -> GRS67 ellipsoidal (radians).
        /// </summary>
        public static (double Latitude, double Longitude, double Height) EovToGrs67(
            double eovY,
            double eovX,
            double height)
        {
            var y = eovY - EovYOrigin;
            var x = eovX - EovXOrigin;

            var fiv = 2 * (Atan(Exp(x / (GaussRadius * M0))) - PI / 4.0);
            var lav = y / (GaussRadius * M0);

            var sf = Cos(F0) * Sin(fiv) + Sin(F0) * Cos(fiv) * Cos(lav);
            var fi = Atan(sf / Sqrt(1 - sf * sf));

            var sl = Sin(lav) * Cos(fiv) / Cos(fi);
            var la = Atan(sl / Sqrt(1 - sl * sl));

            var df = (fi - Kfn) * Ro;
            var dnf = (Av * df - Bv * df * df + Cv * df * df * df) / Ro;
            var nf = Nfn + dnf;
            var nl = L0 + la / K2;

            return (nf, nl, height);
        }

        /// <summary>
        /// GRS67 ellipsoidal (radians) -> EOV projected.
        /// </summary>
        public static (double EovY, double EovX, double Height) Grs67ToEov(
            double latitude,
            double longitude,
            double height)
        {
            var df = (latitude - Nfn) * Ro;
            df = (An * df + Bn * df * df - Cn * df * df * df) / Ro;
            var dl = longitude - L0;
            var f = Kfn + df;
            var l = K2 * dl;

            var fiv = Sin(f) * Cos(F0) - Cos(f) * Sin(F0) * Cos(l);
            fiv = Atan(fiv / Sqrt(1 - fiv * fiv));

            var lav = Cos(f) * Sin(l) / Cos(fiv);
            lav = Atan(lav / Sqrt(1 - lav * lav));

            var xeov = GaussRadius * M0 * Log(Tan(PI / 4.0 + fiv / 2.0)) + EovXOrigin;
            var yeov = GaussRadius * M0 * lav + EovYOrigin;

            return (yeov, xeov, height);
        }

    }
}
