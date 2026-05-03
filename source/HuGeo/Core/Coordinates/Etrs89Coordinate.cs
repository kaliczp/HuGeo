namespace HuGeo.Core.Coordinates;

public record Etrs89Coordinate(double Latitude, double Longitude, double Height = 0.0) : ICoordinate
{
    private const double MinLatitude = -90.0;
    private const double MaxLatitude = 90.0;
    private const double MinLongitude = -180.0;
    private const double MaxLongitude = 180.0;

    public bool IsValid()
    {
        return Latitude >= MinLatitude && Latitude <= MaxLatitude &&
               Longitude >= MinLongitude && Longitude <= MaxLongitude;
    }

    public string CoordinateSystemName => "ETRS89";

    public void Validate()
    {
        if (!IsValid())
            throw new InvalidOperationException(
                $"Invalid ETRS89 coordinate: Lat={Latitude:F6}, Lon={Longitude:F6}. " +
                $"Valid ranges: Lat=[{MinLatitude}, {MaxLatitude}], Lon=[{MinLongitude}, {MaxLongitude}]");
    }

    public override string ToString()
    {
        return $"ETRS89(Lat={Latitude:F6}, Lon={Longitude:F6}, H={Height:F2})";
    }
}
