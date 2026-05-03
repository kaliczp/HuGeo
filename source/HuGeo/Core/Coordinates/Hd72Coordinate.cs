namespace HuGeo.Core.Coordinates;

public record Hd72Coordinate(double Easting, double Northing, double Height = 0.0) : ICoordinate
{
    // Broad operational EOV bounds, not a cadastral Hungary polygon check.
    // EOV convention in this library: Y = Easting, X = Northing.
    private const double MinEasting = 200000;
    private const double MaxEasting = 960000;
    private const double MinNorthing = 0;
    private const double MaxNorthing = 430000;

    public bool IsValid()
    {
        return Easting >= MinEasting && Easting <= MaxEasting &&
               Northing >= MinNorthing && Northing <= MaxNorthing;
    }

    public string CoordinateSystemName => "HD72 (EOV)";

    public void Validate()
    {
        if (!IsValid())
            throw new InvalidOperationException(
                $"Invalid HD72 coordinate: E={Easting}, N={Northing}. " +
                $"Valid ranges: E=[{MinEasting}, {MaxEasting}], N=[{MinNorthing}, {MaxNorthing}]");
    }

    public override string ToString()
    {
        return $"HD72(E={Easting:F2}, N={Northing:F2}, H={Height:F2})";
    }
}
