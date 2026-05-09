using HuGeo.Core.Coordinates;

namespace HuGeo.Tests.Transformations;

public class OfficialFixtureValidationTests
{
    [Fact]
    public void ExtendedForwardFixture_PointsStayInsideHungaryBoundary()
    {
        var points = OfficialFixtureData.LoadExtendedForwardPoints();

        Assert.True(points.Count >= 1900, "Extended official fixture should contain ~2000 points.");
        Assert.All(points, point =>
        {
            var coordinate = new Wgs84Coordinate(point.ExpectedLat, point.ExpectedLon, point.ExpectedH);
            Assert.True(
                coordinate.IsInHungary(),
                $"Fixture point {point.PointNumber} is outside Hungary: lat={point.ExpectedLat:F10}, lon={point.ExpectedLon:F10}");
        });
    }

    [Fact]
    public void ExtendedReverseFixture_PointsStayInsideHungaryBoundary()
    {
        var points = OfficialFixtureData.LoadExtendedReversePoints();

        Assert.True(points.Count >= 1900, "Extended reverse official fixture should contain ~2000 points.");
        Assert.All(points, point =>
        {
            var coordinate = new Wgs84Coordinate(point.Latitude, point.Longitude, point.Height);
            Assert.True(
                coordinate.IsInHungary(),
                $"Reverse fixture point {point.PointNumber} is outside Hungary: lat={point.Latitude:F10}, lon={point.Longitude:F10}");
        });
    }
}
