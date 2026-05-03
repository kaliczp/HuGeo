namespace HuGeo.Core.Ellipsoids;

public record EllipsoidParameters(
    double SemiMajorAxis,
    double SemiMinorAxis,
    string Name)
{
    public double Flattening => (SemiMajorAxis - SemiMinorAxis) / SemiMajorAxis;

    public double FirstEccentricitySquared
    {
        get
        {
            double e2 = 2 * Flattening - Flattening * Flattening;
            return e2;
        }
    }

    [Obsolete("Use FirstEccentricitySquared. This property returns e^2, not e.")]
    public double NumericEccentricity => FirstEccentricitySquared;

    public double PolarRadius => SemiMinorAxis;

    public override string ToString() => $"{Name} (a={SemiMajorAxis}, b={SemiMinorAxis})";
}
