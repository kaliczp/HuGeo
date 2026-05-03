using static System.Math;

namespace HuGeo.Core.Math
{
    /// <summary>
    /// Legacy 7-parameter Helmert transformation used only by the compatibility
    /// GridWithFallback/HelmertOnly pipeline.
    /// </summary>
    /// <remarks>
    /// Parameters: dx=-44.338 m, dy=75.969 m, dz=0.517 m,
    /// rx=-0.443", ry=0.402", rz=-0.238", scale=0.99999847.
    /// These values are retained as historical project parameters for regression
    /// compatibility with the pre-official-grid implementation. They are not the
    /// recommended production path. Production survey workflows should use
    /// TransformationMode.OfficialGrid, where the BME/PROJ/EHT correction grid and
    /// geoid supply the centimeter-relevant HD72/EOV <-> ETRS89 transformation.
    /// </remarks>
    public class HelmertTransformation
    {
        private readonly double _dx = -44.338;
        private readonly double _dy = 75.969;
        private readonly double _dz = 0.517;
        private readonly double _rx = -0.443;
        private readonly double _ry = 0.402;
        private readonly double _rz = -0.238;
        private readonly double _scale = 0.99999847;

        private static readonly double ArcSecondsToRadians = PI / (180.0 * 3600.0);

        public (double X, double Y, double Z) TransformGrs67ToWgs84(double x, double y, double z)
        {
            var m = CreateRotationMatrix();
            var tx = x - _dx;
            var ty = y - _dy;
            var tz = z - _dz;

            // Inverse Helmert: remove translation/scale, then multiply by R^T.
            var (resultX, resultY, resultZ) = MultiplyTranspose(m, tx, ty, tz);

            return (resultX / _scale, resultY / _scale, resultZ / _scale);
        }

        public (double X, double Y, double Z) TransformWgs84ToGrs67(double x, double y, double z)
        {
            var m = CreateRotationMatrix();
            var (rx, ry, rz) = Multiply(m, x, y, z);

            return (
                _dx + _scale * rx,
                _dy + _scale * ry,
                _dz + _scale * rz);
        }

        private (double M11, double M12, double M13, double M21, double M22, double M23, double M31, double M32, double M33) CreateRotationMatrix()
        {
            var frx = _rx * ArcSecondsToRadians;
            var fry = _ry * ArcSecondsToRadians;
            var frz = _rz * ArcSecondsToRadians;

            return (
                Cos(fry) * Cos(frz),
                Cos(fry) * Sin(frz),
                -Sin(fry),

                Sin(frx) * Sin(fry) * Cos(frz) - Cos(frx) * Sin(frz),
                Sin(frx) * Sin(fry) * Sin(frz) + Cos(frx) * Cos(frz),
                Sin(frx) * Cos(fry),

                Cos(frx) * Sin(fry) * Cos(frz) + Sin(frx) * Sin(frz),
                Cos(frx) * Sin(fry) * Sin(frz) - Sin(frx) * Cos(frz),
                Cos(frx) * Cos(fry));
        }

        private static (double X, double Y, double Z) Multiply(
            (double M11, double M12, double M13, double M21, double M22, double M23, double M31, double M32, double M33) m,
            double x,
            double y,
            double z) =>
            (
                m.M11 * x + m.M12 * y + m.M13 * z,
                m.M21 * x + m.M22 * y + m.M23 * z,
                m.M31 * x + m.M32 * y + m.M33 * z);

        private static (double X, double Y, double Z) MultiplyTranspose(
            (double M11, double M12, double M13, double M21, double M22, double M23, double M31, double M32, double M33) m,
            double x,
            double y,
            double z) =>
            (
                m.M11 * x + m.M21 * y + m.M31 * z,
                m.M12 * x + m.M22 * y + m.M32 * z,
                m.M13 * x + m.M23 * y + m.M33 * z);

        [Obsolete("Use TransformGrs67ToWgs84. The old Forward/Reverse names are ambiguous.")]
        public (double X, double Y, double Z) TransformForward(double x, double y, double z) =>
            TransformGrs67ToWgs84(x, y, z);

        [Obsolete("Use TransformWgs84ToGrs67. The old Forward/Reverse names are ambiguous.")]
        public (double X, double Y, double Z) TransformReverse(double x, double y, double z) =>
            TransformWgs84ToGrs67(x, y, z);
    }
}
