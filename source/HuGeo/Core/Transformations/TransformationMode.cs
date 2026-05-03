namespace HuGeo.Core.Transformations;

public enum TransformationMode
{
    [Obsolete("Use the explicit legacy TECA path or OfficialGrid.")]
    GridBased,
    [Obsolete("Use the explicit legacy TECA path or OfficialGrid.")]
    HelmertOnly,
    [Obsolete("Use the explicit legacy TECA path or OfficialGrid.")]
    GridWithFallback,
    OfficialGrid
}
