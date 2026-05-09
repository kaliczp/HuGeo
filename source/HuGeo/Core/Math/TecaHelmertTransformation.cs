namespace HuGeo.Core.Math
{
    /// <summary>
    /// 7-paraméteres Helmert-transzformáció TECA (Brolly Gábor) paramétereivel.
    /// Supported by the TECA compatibility path.
    /// Forward: WGS84 geocentric → IUGG67 (GRS67) geocentric
    /// Reverse: IUGG67 geocentric → WGS84 geocentric
    /// </summary>
    public class TecaHelmertTransformation
    {
        // Eltolás WGS84 → IUGG67
        private const double Dx = -54.595;
        private const double Dy =  72.495;
        private const double Dz =  14.817;

        // Méretarány
        private const double M = 1.0 - 1.998606e-6;

        // Forgató mátrix (C-tömb indexelés: mr[sor][oszlop]) — pontosan a TECA kódból
        private static readonly double[,] Mr = new double[3, 3]
        {
            {  0.99999999999868900000,  0.00000141867263403686, -0.00000078073425578426 },
            { -0.00000141867148993435,  0.99999999999792000000,  0.00000146541722506840 },
            {  0.00000078073633472995, -0.00000146541611746105,  0.99999999999862200000 }
        };

        /// <summary>
        /// WGS84 geocentric → IUGG67 geocentric.
        /// TECA képlet: X_iugg = d + m * (Mr^T * X_wgs)
        /// ahol Mr^T-t a TECA kód oszlopvektoros szorzásként alkalmaz.
        /// </summary>
        public (double X, double Y, double Z) TransformWgs84ToIugg67(double x, double y, double z)
        {
            var xi = Dx + M * (Mr[0, 0] * x + Mr[1, 0] * y + Mr[2, 0] * z);
            var yi = Dy + M * (Mr[0, 1] * x + Mr[1, 1] * y + Mr[2, 1] * z);
            var zi = Dz + M * (Mr[0, 2] * x + Mr[1, 2] * y + Mr[2, 2] * z);
            return (xi, yi, zi);
        }

        /// <summary>
        /// IUGG67 geocentric → WGS84 geocentric (inverz Helmert).
        /// X_wgs = Mr * (X_iugg - d) / m
        /// </summary>
        public (double X, double Y, double Z) TransformIugg67ToWgs84(double x, double y, double z)
        {
            var tx = (x - Dx) / M;
            var ty = (y - Dy) / M;
            var tz = (z - Dz) / M;

            var xw = Mr[0, 0] * tx + Mr[0, 1] * ty + Mr[0, 2] * tz;
            var yw = Mr[1, 0] * tx + Mr[1, 1] * ty + Mr[1, 2] * tz;
            var zw = Mr[2, 0] * tx + Mr[2, 1] * ty + Mr[2, 2] * tz;
            return (xw, yw, zw);
        }

        [Obsolete("Use TransformWgs84ToIugg67. The old Forward/Reverse names are ambiguous.")]
        public (double X, double Y, double Z) TransformForward(double x, double y, double z) =>
            TransformWgs84ToIugg67(x, y, z);

        [Obsolete("Use TransformIugg67ToWgs84. The old Forward/Reverse names are ambiguous.")]
        public (double X, double Y, double Z) TransformReverse(double x, double y, double z) =>
            TransformIugg67ToWgs84(x, y, z);
    }
}
