using HuGeo.Api;
using HuGeo.Core.Coordinates;
using Microsoft.Extensions.DependencyInjection;

namespace HuGeo.Tests.Api;

public class CoordinateTransformerApiTests
{
    [Fact]
    public async Task AddHuGeo_RegistersReadyTransformer()
    {
        var services = new ServiceCollection();
        services.AddHuGeo();

        using var provider = services.BuildServiceProvider();
        var transformer = provider.GetRequiredService<ICoordinateTransformer>();
        var legacyTransformer = provider.GetRequiredService<ILegacyCoordinateTransformer>();

        Assert.True(transformer.IsReady);
        Assert.Same(transformer, legacyTransformer);
        var etrs89 = transformer.TransformToEtrs89(new Wgs84Coordinate(47.5, 19.0, 120));
        Assert.Equal(47.5, etrs89.Latitude);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task TransformBatchAsync_PropagatesCancellation()
    {
        var transformer = await TransformerFactory.CreateSurveyGradeAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            transformer.TransformToEtrs89BatchAsync(
                new[] { new Wgs84Coordinate(47.5, 19.0, 120) },
                cts.Token));
    }

    [Fact]
    public async Task TransformBatch_PreservesInputOrder()
    {
        var transformer = await TransformerFactory.CreateSurveyGradeAsync();
        var input = new[]
        {
            new Wgs84Coordinate(47.5, 19.0, 120),
            new Wgs84Coordinate(46.8, 18.2, 130)
        };

        var output = transformer.TransformToEtrs89Batch(input);

        Assert.Equal(input.Length, output.Count);
        Assert.Equal(input[0].Latitude, output[0].Latitude);
        Assert.Equal(input[1].Longitude, output[1].Longitude);
    }

    [Fact]
    public void Dispose_MakesTransformerUnusable()
    {
        var transformer = new CoordinateTransformer();
        transformer.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            transformer.TransformToEtrs89(new Wgs84Coordinate(47.5, 19.0, 120)));
    }

    [Fact]
    public async Task FastEovToEtrs89_MatchesObjectApi()
    {
        var transformer = (CoordinateTransformer)await TransformerFactory.CreateSurveyGradeAsync();
        var source = new[]
        {
            new EovPoint(650000, 200000, 120),
            new EovPoint(700000, 250000, 130)
        };
        var destination = new Etrs89Point[source.Length];

        var written = transformer.TransformEovToEtrs89(source, destination);

        Assert.Equal(source.Length, written);
        for (var i = 0; i < written; i++)
        {
            var expected = transformer.TransformToEtrs89(new Hd72Coordinate(source[i].Easting, source[i].Northing, source[i].Height));
            Assert.InRange(destination[i].Latitude, expected.Latitude - 1e-12, expected.Latitude + 1e-12);
            Assert.InRange(destination[i].Longitude, expected.Longitude - 1e-12, expected.Longitude + 1e-12);
            Assert.InRange(destination[i].Height, expected.Height - 1e-6, expected.Height + 1e-6);
        }
    }

    [Fact]
    public async Task FastEtrs89ToEov_MatchesObjectApi()
    {
        var transformer = (CoordinateTransformer)await TransformerFactory.CreateSurveyGradeAsync();
        var source = new[]
        {
            new Etrs89Point(47.144521597, 19.048457563, 160),
            new Etrs89Point(47.6, 19.6, 150)
        };
        var destination = new EovPoint[source.Length];

        var written = transformer.TransformEtrs89ToEov(source, destination);

        Assert.Equal(source.Length, written);
        for (var i = 0; i < written; i++)
        {
            var expected = transformer.TransformToEov(new Etrs89Coordinate(source[i].Latitude, source[i].Longitude, source[i].Height));
            Assert.InRange(destination[i].Easting, expected.Easting - 1e-6, expected.Easting + 1e-6);
            Assert.InRange(destination[i].Northing, expected.Northing - 1e-6, expected.Northing + 1e-6);
            Assert.InRange(destination[i].Height, expected.Height - 1e-6, expected.Height + 1e-6);
        }
    }
}
