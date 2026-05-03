namespace HuGeo.Core.Coordinates;

/// <summary>
/// Allocation-free EOV/HD72 point for high-volume transformations.
/// EOV convention: Y = Easting, X = Northing.
/// </summary>
public readonly record struct EovPoint(double Easting, double Northing, double Height = 0.0);
