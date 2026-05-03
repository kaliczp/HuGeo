namespace HuGeo.Core.Ellipsoids;

public static class WGS84
{
    public static readonly EllipsoidParameters Parameters = new(
        SemiMajorAxis: 6378137,
        SemiMinorAxis: 6356752.31425,
        Name: "WGS84 (World Geodetic System)")
    {
    };
}
