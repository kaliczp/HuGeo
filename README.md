# HuGeo

HuGeo is a Hungarian coordinate transformation library focused on `HD72 / EOV`, `ETRS89`, and `WGS84` workflows.

The project currently supports two transformation branches:

- `Official`: the recommended survey-grade workflow based on the official horizontal correction grid and official geoid model.
- `TECA`: a supported legacy and compatibility workflow based on the historical TECA grid and Helmert parameters.

## Scope

The library is designed for Hungarian national coordinate transformation use cases where the main practical problem is transforming between:

- `HD72 / EOV`
- `ETRS89`
- `WGS84` as GNSS-style input/output

The explicit survey-grade path is:

- `HD72 / EOV -> ETRS89`
- `ETRS89 -> HD72 / EOV`

The `WGS84 <-> ETRS89` step is intentionally modeled as a type-level no-op in the current Hungarian workflow. The library assumes the incoming GNSS `WGS84` coordinate is already compatible with the `ETRS89` realization expected by the official EHT/PROJ-based transformation workflow. Epoch-dependent realization handling is outside the scope of this codebase.

## Transformation Branches

### Official branch

The `Official` branch uses:

- the official horizontal correction grid
- the official geoid grid for height handling
- explicit `ETRS89` intermediate coordinates

At a high level, the forward survey-grade computation is:

1. Convert `HD72 / EOV` projected coordinates to geographic `GRS67` latitude, longitude, and ellipsoidal height.
2. Evaluate the official horizontal correction grid in the `HD72 / GRS67` geographic frame.
3. Apply the official horizontal correction in arc-seconds.
4. Evaluate the official geoid model and apply the vertical correction.
5. Return the result as `ETRS89`.

The reverse survey-grade computation is:

1. Start from `ETRS89`.
2. Sample the official horizontal correction grid.
3. Subtract the official horizontal correction to estimate the `HD72 / GRS67` geographic coordinate.
4. Evaluate the geoid correction at that estimated `HD72` position.
5. Project the corrected `HD72 / GRS67` coordinate back to `EOV`.

This is the branch that should be used when the goal is modern, survey-grade transformation quality.

### TECA branch

The `TECA` branch is a supported compatibility path. It remains useful for:

- reproducing legacy behavior
- interoperability with older workflows
- regression comparison against older transformation logic

It uses:

- the historical `grid_delta.dat` TECA grid
- TECA-style bilinear interpolation
- TECA Helmert parameters

It is still supported, but it is not the recommended branch when the goal is the best possible agreement with the official EHT service.

## Benchmark Methodology

The current benchmark is built from an official-reference nationwide point set.

Benchmark date:

- `2026-05-09`

Reference source:

- official EHT service at [eht.gnssnet.hu](https://eht.gnssnet.hu/)

Benchmark generation process:

1. Load the Hungary country polygon from `hungary.geojson`.
2. Generate `2000` stratified random test points strictly inside the Hungary boundary polygon.
3. Query the official EHT service for:
   - `ETRS89 -> EOV`
   - `EOV -> ETRS89`
4. Save the returned official reference pairs as benchmark fixtures.
5. Run the local `Official` and `TECA` branches against the same point set.
6. Measure horizontal and height error against the official reference output.

This is important because the benchmark is not based on a rectangular bounding box anymore. The current fixture is polygon-constrained and validated to remain inside Hungary.

Benchmark assets:

- [source/HuGeo.Test/TestData/Official/eov-etrs89-official-extended.txt](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Official/eov-etrs89-official-extended.txt)
- [source/HuGeo.Test/TestData/Official/etrs89-eov-official-extended.txt](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Official/etrs89-eov-official-extended.txt)
- [source/HuGeo.Test/TestData/Boundary/hungary.geojson](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Boundary/hungary.geojson)
- [tools/Generate-NationwideOfficialBenchmark.py](/D:/Repositories/Primusz/HuGeo/tools/Generate-NationwideOfficialBenchmark.py)

## Accuracy Summary

The following tables summarize agreement against the official EHT reference service on the `2000`-point nationwide benchmark.

### Horizontal Accuracy

Forward direction: `HD72 / EOV -> ETRS89 / WGS84`

| Branch | Average 2D error (m) | Max 2D error (m) | P95 2D error (m) | P99 2D error (m) |
| --- | ---: | ---: | ---: | ---: |
| Official | 0.003804 | 0.019998 | 0.007556 | 0.010616 |
| TECA | 0.014069 | 0.058547 | 0.028385 | 0.038579 |

Reverse direction: `ETRS89 / WGS84 -> HD72 / EOV`

| Branch | Average 2D error (m) | Max 2D error (m) | P95 2D error (m) | P99 2D error (m) |
| --- | ---: | ---: | ---: | ---: |
| Official | 0.004531 | 0.020083 | 0.008341 | 0.011399 |
| TECA | 0.014037 | 0.058823 | 0.028348 | 0.038144 |

### Height Accuracy

Forward direction: `HD72 / EOV -> ETRS89 / WGS84`

| Branch | Average height error (m) | Max height error (m) |
| --- | ---: | ---: |
| Official | 0.001290 | 0.007679 |
| TECA | 0.043516 | 0.312461 |

Reverse direction: `ETRS89 / WGS84 -> HD72 / EOV`

| Branch | Average height error (m) | Max height error (m) |
| --- | ---: | ---: |
| Official | 0.001290 | 0.007679 |
| TECA | 0.043516 | 0.312461 |

### Coordinate-Component Detail

Official forward component error on the same nationwide benchmark:

| Component | Average | Maximum |
| --- | ---: | ---: |
| Latitude error (deg) | 1.5589856335651575e-08 | 1.2036371543899804e-07 |
| Longitude error (deg) | 4.0763648545549815e-08 | 2.3865338505402178e-07 |
| Height error (m) | 0.0012902700472866044 | 0.0076786255352203625 |

Official reverse component error on the same nationwide benchmark:

| Component | Average (m) | Maximum (m) |
| --- | ---: | ---: |
| Easting error | 0.0038502741238626184 | 0.019307280774228275 |
| Northing error | 0.0017596489336501691 | 0.014613072766223922 |
| Height error | 0.0012902296509517371 | 0.0076786192492761529 |

### Round-Trip Consistency

Official round-trip consistency, measured as:

- `EOV -> ETRS89 -> EOV`

| Metric | Value (m) |
| --- | ---: |
| Average round-trip error | 0.001446 |
| Max round-trip error | 0.002789 |
| P95 round-trip error | 0.002053 |

## Interpretation

The current benchmark shows:

- the `Official` branch consistently matches the official EHT reference at the millimeter-to-low-centimeter level
- the `TECA` branch remains good for legacy compatibility, but is materially less accurate than the official branch in both horizontal and height terms
- the height difference is especially important: the official branch stays near millimeter-level average agreement, while `TECA` is in the centimeter range on average and can reach decimeter-level worst cases

## Grid Coverage

For the `2000` nationwide benchmark points:

- `HD72` correction grid rectangular extent covers `100%` of benchmark points
- `Geoid` grid rectangular extent covers `100%` of benchmark points

Grid extent summary:

| Grid | Longitude extent | Latitude extent |
| --- | --- | --- |
| HD72 correction grid | `16.1111 .. 23.0556` | `45.5556 .. 48.8889` |
| Geoid grid | `16.1000 .. 23.0420` | `45.5600 .. 48.8900` |

Operational note:

- the benchmark point set is fully covered
- a few extreme western country-boundary vertices lie slightly outside the rectangular grid extents
- therefore the practical nationwide benchmark is covered, but the rectangular grid bbox is not a mathematically exact superset of every boundary vertex

## Tests

The project includes dedicated tests for benchmark validity, transformation quality, and regression comparison.

### Core validation

- `CoordinateValidationTests`
  Validates coordinate-domain behavior, including polygon-based `IsInHungary()`.

### Official benchmark and fixture validation

- `OfficialFixtureValidationTests`
  Confirms that the nationwide benchmark fixture remains inside the Hungary polygon.

- `OfficialGridAccuracyTests`
  Measures both `Official` and `TECA` against the official EHT reference on the nationwide benchmark.

- `OfficialEhtExtendedFixtureTests`
  Validates forward, reverse, percentile, outlier, and round-trip behavior on the extended nationwide fixture.

- `OfficialEhtWebReferenceTests`
  Verifies agreement against the official fixture and service-oriented reference data.

### Branch comparison and regression

- `LegacyVsOfficialComparisonTests`
  Confirms that the `Official` branch beats the `TECA` branch on the same official benchmark.

- `TecaAccuracyTests`
  Measures TECA-specific accuracy and compatibility behavior.

### Full test status

Current result:

- `57 / 57` tests passing

## Visualizations

Benchmark points sampled inside the Hungary boundary:

![Nationwide benchmark points](/D:/Repositories/Primusz/HuGeo/nationwide-official-benchmark-points.png)

Hungary boundary, benchmark points, and correction grid coverage:

![Grid coverage](/D:/Repositories/Primusz/HuGeo/nationwide-grid-coverage.png)
