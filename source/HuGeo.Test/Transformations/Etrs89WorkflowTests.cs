using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;

namespace HuGeo.Tests.Transformations;

public class Etrs89WorkflowTests : IAsyncLifetime
{
    private ILegacyCoordinateTransformer _transformer = null!;

    public async Task InitializeAsync()
    {
        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        _transformer = new CoordinateTransformer(repo);
        await _transformer.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Wgs84ToEtrs89_IsExplicitAndPreservesCoordinates()
    {
        var wgs84 = new Wgs84Coordinate(47.4979, 19.0402, 123.4);

        var etrs89 = _transformer.TransformToEtrs89(wgs84);

        Assert.Equal(wgs84.Latitude, etrs89.Latitude, 12);
        Assert.Equal(wgs84.Longitude, etrs89.Longitude, 12);
        Assert.Equal(wgs84.Height, etrs89.Height, 12);
        Assert.Equal("ETRS89", etrs89.CoordinateSystemName);
    }

    [Fact]
    public void Wgs84ToEtrs89_ThenToEov_UsesExplicitWorkflow()
    {
        var wgs84 = new Wgs84Coordinate(47.4979, 19.0402, 123.4);

        var etrs89 = _transformer.TransformToEtrs89(wgs84);
        var eov = _transformer.TransformToEov(etrs89);

        var legacy = _transformer.Transform(wgs84);

        Assert.True(eov.IsValid());
        Assert.Equal(legacy.Easting, eov.Easting, 6);
        Assert.Equal(legacy.Northing, eov.Northing, 6);
        Assert.Equal(legacy.Height, eov.Height, 6);
    }

    [Fact]
    public void EovToEtrs89ToWgs84_UsesExplicitWorkflow()
    {
        var wgs84 = new Wgs84Coordinate(47.4979, 19.0402, 123.4);
        var hd72 = _transformer.Transform(wgs84);

        var etrs89 = _transformer.TransformToEtrs89(hd72);
        var roundTrip = _transformer.TransformToWgs84(etrs89);
        var direct = _transformer.TransformToWgs84(hd72);

        Assert.Equal(etrs89.Latitude, roundTrip.Latitude, 12);
        Assert.Equal(etrs89.Longitude, roundTrip.Longitude, 12);
        Assert.Equal(etrs89.Height, roundTrip.Height, 12);
        Assert.Equal(roundTrip.Latitude, direct.Latitude, 12);
        Assert.Equal(roundTrip.Longitude, direct.Longitude, 12);
        Assert.Equal(roundTrip.Height, direct.Height, 12);
    }
}
