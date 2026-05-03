namespace HuGeo.DataAccess.Models;

public record Wgs84GridPoint(
    double Latitude,
    double Longitude,
    double DeltaEasting,
    double DeltaNorthing,
    double DeltaHeight);
