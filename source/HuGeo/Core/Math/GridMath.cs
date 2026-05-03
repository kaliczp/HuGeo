namespace HuGeo.Core.Math;

internal static class GridMath
{
    internal static double Bilinear(double q00, double q10, double q01, double q11, double tx, double ty)
    {
        var r0 = q00 * (1 - tx) + q10 * tx;
        var r1 = q01 * (1 - tx) + q11 * tx;
        return r0 * (1 - ty) + r1 * ty;
    }
}
