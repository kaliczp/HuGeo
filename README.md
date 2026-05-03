# HuGeo

HuGeo is a Hungarian coordinate transformation library for:

- `WGS84` to `ETRS89`
- `ETRS89` to `EOV` / `HD72`
- `WGS84` to `EOV` / `HD72`
- legacy TECA regression workflows

The modern survey-grade path is:

```text
WGS84 -> ETRS89 -> EOV/HD72
```

`WGS84 -> ETRS89` is an explicit no-op type step in this library. The cm-level
accuracy comes from the official horizontal grid and geoid in the `ETRS89 <-> EOV/HD72`
step.

The legacy TECA reference used by the regression tests is based on the original
Soproni Egyetem application by Brolly Gábor:

- `TECA: TÉrbeli Coordináta Átszámító alkalmazás`
- `Brolly Gábor`
- `Koordináta transzformáció: WGS84 --> EOV országos paraméterkészlet`
- `Az alkalmazás a maradékokkal javítja az országos transzformációt`

The modern official solution used by HuGeo is:

```text
WGS84 -> ETRS89 -> EOV/HD72
```

Step summary:

1. `WGS84 -> ETRS89`
   - explicit type step in HuGeo
   - no time-dependent datum model is applied
2. `ETRS89 -> EOV/HD72`
   - official horizontal correction grid is applied
   - official geoid correction is applied for height
3. `EOV/HD72 -> ETRS89 -> WGS84`
   - reverse workflow uses the same official resources in the opposite direction

Accuracy:

- The official grid workflow is the production path and is the one intended for survey-grade use.
- The original grid data is designed to match the official EHT / PROJ workflow.
- For the official test fixtures, the implementation is validated at the centimeter level and is expected to stay within that range on covered points.
- The legacy TECA path remains only as a regression reference and should not be used as the preferred production route.

## Status

- .NET `8.0`
- packable NuGet library
- official binary grid resources embedded in the package
- legacy TECA compatibility kept for regression testing

## Quick Start

```csharp
var transformer = await TransformerFactory.CreateSurveyGradeAsync();

var wgs84 = new Wgs84Coordinate(47.4979, 19.0402, 123.4);
var etrs89 = transformer.TransformToEtrs89(wgs84);
var eov = transformer.TransformToEov(etrs89);
```

Reverse direction:

```csharp
var eov = new Hd72Coordinate(650000, 200000, 123.4);
var etrs89 = transformer.TransformToEtrs89(eov);
var wgs84 = transformer.TransformToWgs84(etrs89);
```

## High-Volume Point Clouds

For 10 to 20 million points, use the allocation-free batch API on the concrete
`CoordinateTransformer` type:

```csharp
var transformer = (CoordinateTransformer)await TransformerFactory.CreateSurveyGradeAsync();

var input = new EovPoint[pointCount];
var output = new Etrs89Point[pointCount];

var written = transformer.TransformEovToEtrs89(input, output);
```

Use the span-based methods when you care about throughput:

- `TransformEovToEtrs89(ReadOnlySpan<EovPoint>, Span<Etrs89Point>)`
- `TransformEtrs89ToEov(ReadOnlySpan<Etrs89Point>, Span<EovPoint>)`

These methods avoid per-point heap allocation and are intended for point cloud
and batch GIS processing.

## API Surface

Recommended public entry points:

- `TransformerFactory.CreateSurveyGradeAsync()`
- `ICoordinateTransformer.TransformToEtrs89(...)`
- `ICoordinateTransformer.TransformToEov(...)`
- `CoordinateTransformer.TransformEovToEtrs89(...)`
- `CoordinateTransformer.TransformEtrs89ToEov(...)`
- `services.AddHuGeo()`

Legacy compatibility is still available through `ILegacyCoordinateTransformer`
and the obsolete `Transform(...)` / `TransformAsync(...)` methods.

## Accuracy

- `TransformationMode.OfficialGrid` uses the official correction grid and geoid.
- The official data comes from the public EHT / PROJ sources.
- The legacy TECA path is kept only for comparison and regression checks, and
  is based on the Soproni Egyetem TECA work by Brolly Gábor.
- `WGS84 -> ETRS89` does not apply a time-dependent datum model.
- Real survey accuracy still depends on the GNSS realization and epoch.

## Binary Resources

The official horizontal correction grid and geoid are stored as compact `.hgbin`
embedded resources.

Reference CSV files stay in the repository and can be regenerated from the source
data. The runtime package embeds only the binary grids.

Regenerate the binary resources:

```powershell
.\tools\Generate-BinaryGridResources.ps1
```

## Fixture Generation

Official EHT fixtures can be regenerated with:

```powershell
.\tools\Generate-OfficialEhtFixtures.ps1
```

The script uses the public EHT endpoints and rewrites the official test fixtures
under `source/HuGeo.Test/TestData/Official/`.

## Install

Use the project directly:

```powershell
dotnet test .\source\HuGeo.slnx -c Debug
dotnet pack .\source\HuGeo\HuGeo.csproj -c Debug
```

Or reference the NuGet package once published:

```xml
<PackageReference Include="HuGeo" Version="1.0.0" />
```

## Repository Layout

- `source/HuGeo` - library project
- `source/HuGeo.Test` - regression and accuracy tests
- `source/HuGeo/Api` - public API and DI extension
- `source/HuGeo/Core` - coordinate models and math
- `source/HuGeo/DataAccess` - grid loading and interpolation
- `source/HuGeo/Resources` - embedded grid resources
- `tools` - generation scripts
- `teca` - legacy reference source
- `docs` - background reference material

## Tests

The test suite covers:

- legacy TECA regression checks
- official grid loading
- official EHT web fixtures
- survey-grade forward and reverse workflows
- high-volume API coverage

## Notes

- The public API favors the explicit survey path: `WGS84 -> ETRS89 -> EOV`.
- The legacy API is still present for compatibility, but it is marked obsolete.
- The official grid reverse direction uses a documented approximation that is
  validated by the existing fixtures.
