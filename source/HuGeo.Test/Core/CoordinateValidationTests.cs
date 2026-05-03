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
}
