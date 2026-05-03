namespace HuGeo.Core.Coordinates;

public interface ICoordinate
{
    double Height { get; }
    bool IsValid();
    string CoordinateSystemName { get; }
}
