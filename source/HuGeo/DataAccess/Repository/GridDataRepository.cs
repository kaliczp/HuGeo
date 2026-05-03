using HuGeo.DataAccess.Corrections;
using HuGeo.DataAccess.Loaders;

namespace HuGeo.DataAccess.Repository;

public class GridDataRepository
{
    private readonly IGridDataLoader _loader;
    private readonly OfficialHd72GridLoader _officialLoader;
    private readonly OfficialGeoidGridLoader _officialGeoidLoader;
    private readonly GridCorrectionProvider _correctionProvider;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _isInitialized = false;

    public GridDataRepository(IGridDataLoader? loader = null)
    {
        _loader = loader ?? new EmbeddedResourceGridLoader();
        _officialLoader = new OfficialHd72GridLoader();
        _officialGeoidLoader = new OfficialGeoidGridLoader();
        _correctionProvider = new GridCorrectionProvider();
    }

    public GridCorrectionProvider CorrectionProvider => _correctionProvider;

    public Task InitializeAsync() => InitializeAsync(CancellationToken.None);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
            return;

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            var hd72Points = await _loader.LoadHd72GridAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var wgs84Points = await _loader.LoadWgs84GridAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var officialGrid = _officialLoader.Load();
            cancellationToken.ThrowIfCancellationRequested();
            var officialGeoidGrid = _officialGeoidLoader.Load();

            await _correctionProvider.InitializeAsync(hd72Points, wgs84Points, officialGrid, officialGeoidGrid);
            _isInitialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public bool IsInitialized => _isInitialized;

    public string DataSource => _loader.GetLoadSource();
}
