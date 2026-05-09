namespace HuGeo.Core.Coordinates;

public record Wgs84Coordinate(double Latitude, double Longitude, double Height = 0.0) : ICoordinate
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

    /// <summary>
    /// Checks whether the coordinate falls inside the Hungary country polygon.
    /// This is not just a bounding-box test.
    /// </summary>
    public bool IsInHungary()
    {
        return HungaryBoundary.Contains(Latitude, Longitude);
    }

    public string CoordinateSystemName => "WGS84";

    public void Validate()
    {
        if (!IsValid())
            throw new InvalidOperationException(
                $"Invalid WGS84 coordinate: Lat={Latitude:F6}, Lon={Longitude:F6}. " +
                $"Valid ranges: Lat=[{MinLatitude}, {MaxLatitude}], Lon=[{MinLongitude}, {MaxLongitude}]");
    }

    public override string ToString()
    {
        return $"WGS84(Lat={Latitude:F6}, Lon={Longitude:F6}, H={Height:F2})";
    }
}
