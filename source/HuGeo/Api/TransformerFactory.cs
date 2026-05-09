using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Repository;

namespace HuGeo.Api;

public static class TransformerFactory
{
    /// <remarks>
    /// This method blocks the calling thread. Avoid calling from ASP.NET Core or UI thread contexts
    /// as it may cause a deadlock. Prefer the async counterpart instead.
    /// </remarks>
    public static ICoordinateTransformer CreateDefault()
    {
        var transformer = new CoordinateTransformer(
            gridRepository: null,
            mode: TransformationMode.GridWithFallback);

        transformer.InitializeAsync().GetAwaiter().GetResult();

        return transformer;
    }

    /// <remarks>
    /// This method blocks the calling thread. Avoid calling from ASP.NET Core or UI thread contexts
    /// as it may cause a deadlock. Prefer the async counterpart instead.
    /// </remarks>
    public static ICoordinateTransformer Create(TransformationMode mode = TransformationMode.GridWithFallback)
    {
        var transformer = new CoordinateTransformer(
            gridRepository: null,
            mode: mode);

        transformer.InitializeAsync().GetAwaiter().GetResult();

        return transformer;
    }

    /// <remarks>
    /// This method blocks the calling thread. Prefer <see cref="CreateSurveyGradeAsync"/>.
    /// </remarks>
    public static ICoordinateTransformer CreateSurveyGrade()
    {
        var transformer = new CoordinateTransformer(
            gridRepository: null,
            mode: TransformationMode.OfficialGrid);

        transformer.InitializeAsync().GetAwaiter().GetResult();

        return transformer;
    }

    public static Task<ICoordinateTransformer> CreateAsync(
        TransformationMode mode = TransformationMode.GridWithFallback) =>
        CreateAsync(mode, CancellationToken.None);

    public static async Task<ICoordinateTransformer> CreateAsync(
        TransformationMode mode,
        CancellationToken cancellationToken)
    {
        var transformer = new CoordinateTransformer(
            gridRepository: null,
            mode: mode);

        await transformer.InitializeAsync(cancellationToken);

        return transformer;
    }

    public static Task<ICoordinateTransformer> CreateSurveyGradeAsync() =>
        CreateSurveyGradeAsync(CancellationToken.None);

    public static async Task<ICoordinateTransformer> CreateSurveyGradeAsync(CancellationToken cancellationToken)
    {
        var transformer = new CoordinateTransformer(
            gridRepository: null,
            mode: TransformationMode.OfficialGrid);

        await transformer.InitializeAsync(cancellationToken);

        return transformer;
    }
}
