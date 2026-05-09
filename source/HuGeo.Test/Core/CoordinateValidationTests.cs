using HuGeo.Core.Coordinates;

namespace HuGeo.Tests.Core;

public class CoordinateValidationTests
{
    [Fact]
    public void Hd72Coordinate_RejectsNegativeNorthing()
    {
        var coordinate = new Hd72Coordinate(650000, -1, 100);

        Assert.False(coordinate.IsValid());
        Assert.Throws<InvalidOperationException>(() => coordinate.Validate());
    }

    [Fact]
    public void Wgs84Coordinate_IsInHungary_UsesPolygonNotOnlyBoundingBox()
    {
        var inside = new Wgs84Coordinate(47.4979, 19.0402, 100);
        var outsideButInOldBounds = new Wgs84Coordinate(45.8, 16.15, 100);

        Assert.True(inside.IsInHungary());
        Assert.False(outsideButInOldBounds.IsInHungary());
    }
}
