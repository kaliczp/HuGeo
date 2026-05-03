namespace HuGeo.Core.Ellipsoids;

public static class GRS67
{
    public static readonly EllipsoidParameters Parameters = new(
        SemiMajorAxis: 6378160,
        SemiMinorAxis: 6356774.516,
        Name: "GRS67 (Hungarian Datum)")
    {
    };
}
