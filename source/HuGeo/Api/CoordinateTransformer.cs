using System.Threading;
using HuGeo.Core.Coordinates;
using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Repository;

namespace HuGeo.Api;

public class CoordinateTransformer : ILegacyCoordinateTransformer, IDisposable
{
    private const string NotInitializedMessage = "Transformer not initialized. Call InitializeAsync first.";

    private readonly GridDataRepository _gridRepository;
    private readonly TransformationContext _transformationContext;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _isInitialized;
    private bool _disposed;

    public CoordinateTransformer(GridDataRepository? gridRepository = null, TransformationMode mode = TransformationMode.GridWithFallback)
    {
        _gridRepository = gridRepository ?? new GridDataRepository();

        _transformationContext = new TransformationContext(
            mode,
            _gridRepository.CorrectionProvider.GetHd72Corrections,
            _gridRepository.CorrectionProvider.GetWgs84Corrections,
            _gridRepository.CorrectionProvider.GetOfficialCorrections,
            _gridRepository.CorrectionProvider.GetOfficialHeightCorrection);
    }

    public bool IsReady => Volatile.Read(ref _isInitialized) && _gridRepository.IsInitialized;

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (Volatile.Read(ref _isInitialized))
            return;

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _isInitialized))
                return;

            await _gridRepository.InitializeAsync(cancellationToken);
            Volatile.Write(ref _isInitialized, true);
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public Etrs89Coordinate TransformToEtrs89(Wgs84Coordinate wgs84)
    {
        EnsureReady();
        return _transformationContext.TransformWgs84ToEtrs89(wgs84);
    }

    public Etrs89Coordinate TransformToEtrs89(Hd72Coordinate hd72)
    {
        EnsureReady();
        return _transformationContext.TransformHd72ToEtrs89(hd72);
    }

    public Wgs84Coordinate TransformToWgs84(Etrs89Coordinate etrs89)
    {
        EnsureReady();
        return _transformationContext.TransformEtrs89ToWgs84(etrs89);
    }

    public Wgs84Coordinate TransformToWgs84(Hd72Coordinate hd72)
    {
        EnsureReady();
        var etrs89 = TransformToEtrs89(hd72);
        return TransformToWgs84(etrs89);
    }

    public Hd72Coordinate TransformToEov(Etrs89Coordinate etrs89)
    {
        EnsureReady();
        return _transformationContext.TransformEtrs89ToHd72(etrs89);
    }

    public Wgs84Coordinate Transform(Hd72Coordinate hd72)
    {
        EnsureReady();
        return _transformationContext.TransformHd72ToWgs84(hd72);
    }

    public Hd72Coordinate Transform(Wgs84Coordinate wgs84)
    {
        EnsureReady();
        return _transformationContext.TransformWgs84ToHd72(wgs84);
    }

    public Task<Etrs89Coordinate> TransformToEtrs89Async(Wgs84Coordinate wgs84) =>
        TransformToEtrs89Async(wgs84, CancellationToken.None);

    /// <remarks>
    /// Uses Task.Run to offload CPU-bound coordinate math. In ASP.NET Core request
    /// pipelines this may be unnecessary; prefer the synchronous batch API when the
    /// caller already controls threading.
    /// </remarks>
    public async Task<Etrs89Coordinate> TransformToEtrs89Async(Wgs84Coordinate wgs84, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformToEtrs89(wgs84), cancellationToken);
    }

    public Task<Etrs89Coordinate> TransformToEtrs89Async(Hd72Coordinate hd72) =>
        TransformToEtrs89Async(hd72, CancellationToken.None);

    /// <remarks>
    /// Uses Task.Run to offload CPU-bound coordinate math. In ASP.NET Core request
    /// pipelines this may be unnecessary; prefer the synchronous batch API when the
    /// caller already controls threading.
    /// </remarks>
    public async Task<Etrs89Coordinate> TransformToEtrs89Async(Hd72Coordinate hd72, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformToEtrs89(hd72), cancellationToken);
    }

    public Task<Wgs84Coordinate> TransformToWgs84Async(Etrs89Coordinate etrs89) =>
        TransformToWgs84Async(etrs89, CancellationToken.None);

    public async Task<Wgs84Coordinate> TransformToWgs84Async(Etrs89Coordinate etrs89, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformToWgs84(etrs89), cancellationToken);
    }

    public Task<Wgs84Coordinate> TransformToWgs84Async(Hd72Coordinate hd72) =>
        TransformToWgs84Async(hd72, CancellationToken.None);

    public async Task<Wgs84Coordinate> TransformToWgs84Async(Hd72Coordinate hd72, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformToWgs84(hd72), cancellationToken);
    }

    public Task<Hd72Coordinate> TransformToEovAsync(Etrs89Coordinate etrs89) =>
        TransformToEovAsync(etrs89, CancellationToken.None);

    public async Task<Hd72Coordinate> TransformToEovAsync(Etrs89Coordinate etrs89, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformToEov(etrs89), cancellationToken);
    }

    public Task<Wgs84Coordinate> TransformAsync(Hd72Coordinate hd72) =>
        TransformAsync(hd72, CancellationToken.None);

    public async Task<Wgs84Coordinate> TransformAsync(Hd72Coordinate hd72, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => Transform(hd72), cancellationToken);
    }

    public Task<Hd72Coordinate> TransformAsync(Wgs84Coordinate wgs84) =>
        TransformAsync(wgs84, CancellationToken.None);

    public async Task<Hd72Coordinate> TransformAsync(Wgs84Coordinate wgs84, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => Transform(wgs84), cancellationToken);
    }

    public IReadOnlyList<Etrs89Coordinate> TransformToEtrs89Batch(IEnumerable<Wgs84Coordinate> coordinates) =>
        TransformBatch(coordinates, TransformToEtrs89);

    public IReadOnlyList<Etrs89Coordinate> TransformToEtrs89Batch(IEnumerable<Hd72Coordinate> coordinates) =>
        TransformBatch(coordinates, TransformToEtrs89);

    public IReadOnlyList<Wgs84Coordinate> TransformToWgs84Batch(IEnumerable<Etrs89Coordinate> coordinates) =>
        TransformBatch(coordinates, TransformToWgs84);

    public IReadOnlyList<Wgs84Coordinate> TransformToWgs84Batch(IEnumerable<Hd72Coordinate> coordinates) =>
        TransformBatch(coordinates, TransformToWgs84);

    public IReadOnlyList<Hd72Coordinate> TransformToEovBatch(IEnumerable<Etrs89Coordinate> coordinates) =>
        TransformBatch(coordinates, TransformToEov);

    public async Task<IReadOnlyList<Etrs89Coordinate>> TransformToEtrs89BatchAsync(IEnumerable<Wgs84Coordinate> coordinates, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformBatch(coordinates, TransformToEtrs89, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<Etrs89Coordinate>> TransformToEtrs89BatchAsync(IEnumerable<Hd72Coordinate> coordinates, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformBatch(coordinates, TransformToEtrs89, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<Wgs84Coordinate>> TransformToWgs84BatchAsync(IEnumerable<Etrs89Coordinate> coordinates, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformBatch(coordinates, TransformToWgs84, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<Wgs84Coordinate>> TransformToWgs84BatchAsync(IEnumerable<Hd72Coordinate> coordinates, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformBatch(coordinates, TransformToWgs84, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<Hd72Coordinate>> TransformToEovBatchAsync(IEnumerable<Etrs89Coordinate> coordinates, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        return await Task.Run(() => TransformBatch(coordinates, TransformToEov, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// High-throughput official EOV/HD72 -> ETRS89 transformation.
    /// This path is allocation-free per point and assumes the transformer is initialized
    /// with the official grid. Points outside official grid/geoid coverage are skipped.
    /// </summary>
    /// <returns>The number of transformed points written to <paramref name="destination"/>.</returns>
    public int TransformEovToEtrs89(ReadOnlySpan<EovPoint> source, Span<Etrs89Point> destination)
    {
        EnsureReady();
        if (destination.Length < source.Length)
            throw new ArgumentException("Destination span is smaller than source span.", nameof(destination));

        var written = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (TryTransformEovToEtrs89(source[i], out var result))
                destination[written++] = result;
        }

        return written;
    }

    /// <summary>
    /// High-throughput official ETRS89 -> EOV/HD72 transformation.
    /// This path is allocation-free per point and assumes the transformer is initialized
    /// with the official grid. Points outside official grid/geoid coverage are skipped.
    /// </summary>
    /// <returns>The number of transformed points written to <paramref name="destination"/>.</returns>
    public int TransformEtrs89ToEov(ReadOnlySpan<Etrs89Point> source, Span<EovPoint> destination)
    {
        EnsureReady();
        if (destination.Length < source.Length)
            throw new ArgumentException("Destination span is smaller than source span.", nameof(destination));

        var written = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (TryTransformEtrs89ToEov(source[i], out var result))
                destination[written++] = result;
        }

        return written;
    }

    public bool TryTransformEovToEtrs89(EovPoint source, out Etrs89Point result)
    {
        EnsureReady();

        var (srcLatRad, srcLonRad, srcH) = HuGeo.Core.Math.GaussProjection.EovToGrs67(source.Easting, source.Northing, source.Height);
        var srcLatDeg = HuGeo.Core.Math.EllipsoidMath.RadiansToDegrees(srcLatRad);
        var srcLonDeg = HuGeo.Core.Math.EllipsoidMath.RadiansToDegrees(srcLonRad);

        var correctionProvider = _gridRepository.CorrectionProvider;
        if (!correctionProvider.TryGetOfficialCorrections(srcLatDeg, srcLonDeg, out var dLatArcSec, out var dLonArcSec) ||
            !correctionProvider.TryGetOfficialHeightCorrection(srcLatDeg, srcLonDeg, out var geoidHeight))
        {
            result = default;
            return false;
        }

        result = new Etrs89Point(
            srcLatDeg + dLatArcSec / 3600.0,
            srcLonDeg + dLonArcSec / 3600.0,
            srcH + geoidHeight);
        return true;
    }

    public bool TryTransformEtrs89ToEov(Etrs89Point source, out EovPoint result)
    {
        EnsureReady();

        var correctionProvider = _gridRepository.CorrectionProvider;
        if (!correctionProvider.TryGetOfficialCorrections(source.Latitude, source.Longitude, out var dLatArcSec, out var dLonArcSec))
        {
            result = default;
            return false;
        }

        var srcLatDeg = source.Latitude - dLatArcSec / 3600.0;
        var srcLonDeg = source.Longitude - dLonArcSec / 3600.0;
        if (!correctionProvider.TryGetOfficialHeightCorrection(srcLatDeg, srcLonDeg, out var geoidHeight))
        {
            result = default;
            return false;
        }

        var eovHeight = source.Height - geoidHeight;
        var (eovY, eovX, _) = HuGeo.Core.Math.GaussProjection.Grs67ToEov(
            HuGeo.Core.Math.EllipsoidMath.DegreesToRadians(srcLatDeg),
            HuGeo.Core.Math.EllipsoidMath.DegreesToRadians(srcLonDeg),
            eovHeight);

        result = new EovPoint(eovY, eovX, eovHeight);
        return true;
    }

    public bool TryTransformToEtrs89(Wgs84Coordinate wgs84, out Etrs89Coordinate? result, out string? error) =>
        TryTransform(() => TransformToEtrs89(wgs84), out result, out error);

    public bool TryTransformToEtrs89(Hd72Coordinate hd72, out Etrs89Coordinate? result, out string? error) =>
        TryTransform(() => TransformToEtrs89(hd72), out result, out error);

    public bool TryTransformToWgs84(Etrs89Coordinate etrs89, out Wgs84Coordinate? result, out string? error) =>
        TryTransform(() => TransformToWgs84(etrs89), out result, out error);

    public bool TryTransformToWgs84(Hd72Coordinate hd72, out Wgs84Coordinate? result, out string? error) =>
        TryTransform(() => TransformToWgs84(hd72), out result, out error);

    public bool TryTransformToEov(Etrs89Coordinate etrs89, out Hd72Coordinate? result, out string? error) =>
        TryTransform(() => TransformToEov(etrs89), out result, out error);

    public bool TryTransform(Hd72Coordinate hd72, out Wgs84Coordinate? result, out string? error) =>
        TryTransform(() => Transform(hd72), out result, out error);

    public bool TryTransform(Wgs84Coordinate wgs84, out Hd72Coordinate? result, out string? error) =>
        TryTransform(() => Transform(wgs84), out result, out error);

    public void Dispose()
    {
        if (_disposed)
            return;

        _initializeLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void EnsureReady()
    {
        ThrowIfDisposed();
        if (!IsReady)
            throw new InvalidOperationException(NotInitializedMessage);
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!IsReady)
            await InitializeAsync(cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CoordinateTransformer));
    }

    private static IReadOnlyList<TOut> TransformBatch<TIn, TOut>(
        IEnumerable<TIn> coordinates,
        Func<TIn, TOut> transform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(transform);

        var results = new List<TOut>();
        foreach (var coordinate in coordinates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(transform(coordinate));
        }

        return results;
    }

    private static bool TryTransform<T>(Func<T> transform, out T? result, out string? error)
    {
        result = default;
        error = null;

        try
        {
            result = transform();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = ex.Message;
            return false;
        }
    }
}
