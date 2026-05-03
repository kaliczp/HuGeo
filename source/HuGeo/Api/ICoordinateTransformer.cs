using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;

namespace HuGeo.Api;

public interface ICoordinateTransformer
{
    /// <summary>
    /// Converts GNSS WGS84 to ETRS89 as an explicit no-op type step. The library assumes
    /// WGS84 input is already ETRS89-compatible for the Hungarian EHT/PROJ workflow.
    /// </summary>
    Etrs89Coordinate TransformToEtrs89(Wgs84Coordinate wgs84);
    Etrs89Coordinate TransformToEtrs89(Hd72Coordinate hd72);

    /// <summary>
    /// Converts ETRS89 to WGS84 as an explicit no-op type step. Epoch-dependent WGS84
    /// realization handling is outside this library.
    /// </summary>
    Wgs84Coordinate TransformToWgs84(Etrs89Coordinate etrs89);
    Wgs84Coordinate TransformToWgs84(Hd72Coordinate hd72);
    Hd72Coordinate TransformToEov(Etrs89Coordinate etrs89);

    Task<Etrs89Coordinate> TransformToEtrs89Async(Wgs84Coordinate wgs84);
    Task<Etrs89Coordinate> TransformToEtrs89Async(Wgs84Coordinate wgs84, CancellationToken cancellationToken);
    Task<Etrs89Coordinate> TransformToEtrs89Async(Hd72Coordinate hd72);
    Task<Etrs89Coordinate> TransformToEtrs89Async(Hd72Coordinate hd72, CancellationToken cancellationToken);
    Task<Wgs84Coordinate> TransformToWgs84Async(Etrs89Coordinate etrs89);
    Task<Wgs84Coordinate> TransformToWgs84Async(Etrs89Coordinate etrs89, CancellationToken cancellationToken);
    Task<Wgs84Coordinate> TransformToWgs84Async(Hd72Coordinate hd72);
    Task<Wgs84Coordinate> TransformToWgs84Async(Hd72Coordinate hd72, CancellationToken cancellationToken);
    Task<Hd72Coordinate> TransformToEovAsync(Etrs89Coordinate etrs89);
    Task<Hd72Coordinate> TransformToEovAsync(Etrs89Coordinate etrs89, CancellationToken cancellationToken);

    IReadOnlyList<Etrs89Coordinate> TransformToEtrs89Batch(IEnumerable<Wgs84Coordinate> coordinates);
    IReadOnlyList<Etrs89Coordinate> TransformToEtrs89Batch(IEnumerable<Hd72Coordinate> coordinates);
    IReadOnlyList<Wgs84Coordinate> TransformToWgs84Batch(IEnumerable<Etrs89Coordinate> coordinates);
    IReadOnlyList<Wgs84Coordinate> TransformToWgs84Batch(IEnumerable<Hd72Coordinate> coordinates);
    IReadOnlyList<Hd72Coordinate> TransformToEovBatch(IEnumerable<Etrs89Coordinate> coordinates);

    Task<IReadOnlyList<Etrs89Coordinate>> TransformToEtrs89BatchAsync(IEnumerable<Wgs84Coordinate> coordinates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Etrs89Coordinate>> TransformToEtrs89BatchAsync(IEnumerable<Hd72Coordinate> coordinates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Wgs84Coordinate>> TransformToWgs84BatchAsync(IEnumerable<Etrs89Coordinate> coordinates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Wgs84Coordinate>> TransformToWgs84BatchAsync(IEnumerable<Hd72Coordinate> coordinates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Hd72Coordinate>> TransformToEovBatchAsync(IEnumerable<Etrs89Coordinate> coordinates, CancellationToken cancellationToken = default);

    bool TryTransformToEtrs89(Wgs84Coordinate wgs84, out Etrs89Coordinate? result, out string? error);
    bool TryTransformToEtrs89(Hd72Coordinate hd72, out Etrs89Coordinate? result, out string? error);
    bool TryTransformToWgs84(Etrs89Coordinate etrs89, out Wgs84Coordinate? result, out string? error);
    bool TryTransformToWgs84(Hd72Coordinate hd72, out Wgs84Coordinate? result, out string? error);
    bool TryTransformToEov(Etrs89Coordinate etrs89, out Hd72Coordinate? result, out string? error);
    bool IsReady { get; }
    Task InitializeAsync();
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface ILegacyCoordinateTransformer : ICoordinateTransformer
{
    [Obsolete("Use TransformToEtrs89 followed by TransformToEov for survey workflows.")]
    Wgs84Coordinate Transform(Hd72Coordinate hd72);

    [Obsolete("Use TransformToEtrs89 followed by TransformToEov for survey workflows.")]
    Hd72Coordinate Transform(Wgs84Coordinate wgs84);

    [Obsolete("Use TransformToEtrs89Async followed by TransformToEovAsync for survey workflows.")]
    Task<Wgs84Coordinate> TransformAsync(Hd72Coordinate hd72);

    [Obsolete("Use TransformToEtrs89Async followed by TransformToEovAsync for survey workflows.")]
    Task<Wgs84Coordinate> TransformAsync(Hd72Coordinate hd72, CancellationToken cancellationToken);

    [Obsolete("Use TransformToEtrs89Async followed by TransformToEovAsync for survey workflows.")]
    Task<Hd72Coordinate> TransformAsync(Wgs84Coordinate wgs84);

    [Obsolete("Use TransformToEtrs89Async followed by TransformToEovAsync for survey workflows.")]
    Task<Hd72Coordinate> TransformAsync(Wgs84Coordinate wgs84, CancellationToken cancellationToken);

    [Obsolete("Use TryTransformToEtrs89 followed by TryTransformToEov for survey workflows.")]
    bool TryTransform(Hd72Coordinate hd72, out Wgs84Coordinate? result, out string? error);

    [Obsolete("Use TryTransformToEtrs89 followed by TryTransformToEov for survey workflows.")]
    bool TryTransform(Wgs84Coordinate wgs84, out Hd72Coordinate? result, out string? error);
}
