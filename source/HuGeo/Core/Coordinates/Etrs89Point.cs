namespace HuGeo.Core.Coordinates;

/// <summary>
/// Allocation-free ETRS89 geodetic point for high-volume transformations.
/// </summary>
public readonly record struct Etrs89Point(double Latitude, double Longitude, double Height = 0.0);
