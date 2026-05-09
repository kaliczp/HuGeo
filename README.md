# HuGeo

Hungarian coordinate transformation library with two supported paths:

- `Official`: survey-grade `HD72/EOV <-> ETRS89` workflow using the official horizontal correction grid and geoid grid.
- `TECA`: supported legacy/compatibility `WGS84 <-> HD72/EOV` workflow based on the historical TECA grid and Helmert parameters.

## Accuracy

The current benchmark uses `2000` points sampled strictly inside the Hungary boundary polygon. Reference values are generated from the official EHT service, then the local `Official` and `TECA` implementations are measured against that ground truth.

Benchmark date: `2026-05-09`

Reference source: official EHT service endpoints at [eht.gnssnet.hu](https://eht.gnssnet.hu/)

### Official vs TECA on the nationwide official benchmark

Forward benchmark (`HD72/EOV -> ETRS89/WGS84`):

- `Official`: avg `0.0038 m`, max `0.0200 m`, p95 `0.0076 m`, p99 `0.0106 m`
- `TECA`: avg `0.0141 m`, max `0.0585 m`, p95 `0.0284 m`, p99 `0.0386 m`

Reverse benchmark (`ETRS89/WGS84 -> HD72/EOV`):

- `Official`: avg `0.0045 m`, max `0.0201 m`, p95 `0.0083 m`, p99 `0.0114 m`
- `TECA`: avg `0.0140 m`, max `0.0588 m`, p95 `0.0283 m`, p99 `0.0381 m`

Height error on the same benchmark:

- `Official`: avg about `0.0013 m`, max about `0.0077 m`
- `TECA`: avg about `0.0435 m`, max about `0.3125 m`

Round-trip consistency for the official path (`EOV -> ETRS89 -> EOV`):

- avg `0.0014 m`
- max `0.0028 m`

## Benchmark Data

The benchmark fixtures are generated from the official EHT API and stored here:

- [source/HuGeo.Test/TestData/Official/eov-etrs89-official-extended.txt](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Official/eov-etrs89-official-extended.txt)
- [source/HuGeo.Test/TestData/Official/etrs89-eov-official-extended.txt](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Official/etrs89-eov-official-extended.txt)
- [source/HuGeo.Test/TestData/Boundary/hungary.geojson](/D:/Repositories/Primusz/HuGeo/source/HuGeo.Test/TestData/Boundary/hungary.geojson)

Generator script:

- [tools/Generate-NationwideOfficialBenchmark.py](/D:/Repositories/Primusz/HuGeo/tools/Generate-NationwideOfficialBenchmark.py)

Fixture validation:

- the generated nationwide fixtures are checked by `OfficialFixtureValidationTests`
- every sampled benchmark point must stay inside the Hungary polygon, not just inside a bounding box

## Plots

Benchmark points sampled inside the Hungary boundary:

![Nationwide benchmark points](/D:/Repositories/Primusz/HuGeo/nationwide-official-benchmark-points.png)

Hungary boundary, benchmark points, and correction grid coverage:

![Grid coverage](/D:/Repositories/Primusz/HuGeo/nationwide-grid-coverage.png)

## Coverage Notes

For the `2000` nationwide benchmark points:

- `HD72` correction grid bbox coverage: `100%`
- `Geoid` grid bbox coverage: `100%`

Grid extent summary:

- `HD72` grid bbox: lon `16.1111..23.0556`, lat `45.5556..48.8889`
- `Geoid` grid bbox: lon `16.1000..23.0420`, lat `45.5600..48.8900`

The benchmark points are fully covered, but a few extreme western boundary vertices of the country polygon fall slightly outside those rectangular grid extents. This means the practical nationwide benchmark is covered, while the grid rectangles are not a perfect superset of every boundary vertex.

## Validation

Relevant tests:

- `OfficialGridAccuracyTests`
- `OfficialEhtExtendedFixtureTests`
- `OfficialEhtWebReferenceTests`
- `LegacyVsOfficialComparisonTests`
- `TecaAccuracyTests`
