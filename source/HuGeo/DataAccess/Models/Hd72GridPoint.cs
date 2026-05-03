namespace HuGeo.DataAccess.Models;

public record Hd72GridPoint(
    double Easting,
    double Northing,
    double DeltaLatitude,
    double DeltaLongitude,
    double DeltaHeight)
{
    [Obsolete("Use Easting. EOV Y is the east-west coordinate.")]
    public double X => Easting;

    [Obsolete("Use Northing. EOV X is the north-south coordinate.")]
    public double Y => Northing;
}
