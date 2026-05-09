#!/usr/bin/env python3
"""
Generate a nationwide official benchmark strictly inside Hungary's boundary.

Outputs:
- source/HuGeo.Test/TestData/Official/eov-etrs89-official-extended.txt
- source/HuGeo.Test/TestData/Official/etrs89-eov-official-extended.txt
- nationwide-official-benchmark-points.png
- nationwide-grid-coverage.png

Ground truth comes from the official EHT service.
"""

from __future__ import annotations

import json
import math
import random
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import matplotlib.pyplot as plt
from matplotlib.path import Path as MplPath


REPO_ROOT = Path(__file__).resolve().parent.parent
BOUNDARY_PATH = REPO_ROOT / "source" / "HuGeo.Test" / "TestData" / "Boundary" / "hungary.geojson"
OFFICIAL_DIR = REPO_ROOT / "source" / "HuGeo.Test" / "TestData" / "Official"
HD72_GRID_PATH = REPO_ROOT / "source" / "HuGeo" / "Resources" / "Resources" / "hu_bme_hd72corr.csv"
GEOID_GRID_PATH = REPO_ROOT / "source" / "HuGeo" / "Resources" / "Resources" / "hu_bme_geoid2014.csv"
FORWARD_PATH = OFFICIAL_DIR / "eov-etrs89-official-extended.txt"
REVERSE_PATH = OFFICIAL_DIR / "etrs89-eov-official-extended.txt"
POINTS_PLOT_PATH = REPO_ROOT / "nationwide-official-benchmark-points.png"
GRID_PLOT_PATH = REPO_ROOT / "nationwide-grid-coverage.png"

REV_URL = "https://eht.gnssnet.hu/api/transformation/etrs89-to-eov"
FWD_URL = "https://eht.gnssnet.hu/api/transformation/eov-to-etrs89"

SEED = 20260509
TARGET_COUNT = 2000
ROWS = 60
COLS = 120
BATCH_SIZE = 25


@dataclass(frozen=True)
class RingPath:
    shell: MplPath
    holes: list[MplPath]

    def contains(self, lon: float, lat: float) -> bool:
        if not self.shell.contains_point((lon, lat)):
            return False
        return not any(hole.contains_point((lon, lat)) for hole in self.holes)


@dataclass(frozen=True)
class HungaryPolygon:
    polygons: list[RingPath]
    bbox: tuple[float, float, float, float]
    outline: list[tuple[list[float], list[float]]]

    def contains(self, lon: float, lat: float) -> bool:
        return any(p.contains(lon, lat) for p in self.polygons)


def load_hungary_polygon(path: Path) -> HungaryPolygon:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    geometry = data["features"][0]["geometry"]
    if geometry["type"] == "Polygon":
        raw_polygons = [geometry["coordinates"]]
    elif geometry["type"] == "MultiPolygon":
        raw_polygons = geometry["coordinates"]
    else:
        raise ValueError(f"Unsupported geometry type: {geometry['type']}")

    polygons: list[RingPath] = []
    outline: list[tuple[list[float], list[float]]] = []
    min_lon = min_lat = float("inf")
    max_lon = max_lat = float("-inf")

    for polygon in raw_polygons:
        shell_coords = polygon[0]
        shell_xy = [(lon, lat) for lon, lat in shell_coords]
        shell = MplPath(shell_xy)
        holes = [MplPath([(lon, lat) for lon, lat in ring]) for ring in polygon[1:]]
        polygons.append(RingPath(shell=shell, holes=holes))

        xs = [lon for lon, _ in shell_xy]
        ys = [lat for _, lat in shell_xy]
        outline.append((xs, ys))
        min_lon = min(min_lon, min(xs))
        max_lon = max(max_lon, max(xs))
        min_lat = min(min_lat, min(ys))
        max_lat = max(max_lat, max(ys))

    return HungaryPolygon(polygons=polygons, bbox=(min_lon, min_lat, max_lon, max_lat), outline=outline)


def post_json(url: str, payload: list[dict]) -> list[dict]:
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read().decode("utf-8"))


def sample_points_inside_hungary(polygon: HungaryPolygon, target_count: int, rows: int, cols: int, seed: int) -> list[tuple[float, float, float]]:
    rng = random.Random(seed)
    min_lon, min_lat, max_lon, max_lat = polygon.bbox
    accepted: list[tuple[float, float, float]] = []
    cell_candidates: list[tuple[int, int]] = [(r, c) for r in range(rows) for c in range(cols)]
    rng.shuffle(cell_candidates)

    def random_point_in_cell(row: int, col: int) -> tuple[float, float]:
        lon0 = min_lon + (max_lon - min_lon) * col / cols
        lon1 = min_lon + (max_lon - min_lon) * (col + 1) / cols
        lat0 = min_lat + (max_lat - min_lat) * row / rows
        lat1 = min_lat + (max_lat - min_lat) * (row + 1) / rows
        return (
            rng.uniform(lon0, lon1),
            rng.uniform(lat0, lat1),
        )

    for row, col in cell_candidates:
        for _ in range(10):
            lon, lat = random_point_in_cell(row, col)
            if polygon.contains(lon, lat):
                height = round(rng.uniform(80.0, 400.0), 3)
                accepted.append((lat, lon, height))
                break
        if len(accepted) >= target_count:
            return accepted

    while len(accepted) < target_count:
        lon = rng.uniform(min_lon, max_lon)
        lat = rng.uniform(min_lat, max_lat)
        if polygon.contains(lon, lat):
            height = round(rng.uniform(80.0, 400.0), 3)
            accepted.append((lat, lon, height))

    return accepted


def generate_reference_points(candidates: list[tuple[float, float, float]]) -> tuple[list[str], list[str], list[tuple[float, float]]]:
    reverse_rows: list[dict] = []
    for index, (lat, lon, height) in enumerate(candidates, start=1):
        reverse_rows.append(
            {
                "pointNumber": f"H{index:05d}",
                "lat": lat,
                "lon": lon,
                "h": height,
                "remark": "",
            }
        )

    accepted_rows: list[dict] = []
    for offset in range(0, len(reverse_rows), BATCH_SIZE):
        batch = reverse_rows[offset : offset + BATCH_SIZE]
        response = post_json(REV_URL, batch)
        for source, result in zip(batch, response):
            if str(result.get("error")) != "0":
                continue
            accepted_rows.append(
                {
                    "pointNumber": source["pointNumber"],
                    "lat": float(source["lat"]),
                    "lon": float(source["lon"]),
                    "h": float(source["h"]),
                    "y": float(result["y"]),
                    "x": float(result["x"]),
                    "eov_h": float(result["h"]),
                }
            )

    accepted_rows = accepted_rows[:TARGET_COUNT]
    if len(accepted_rows) < TARGET_COUNT:
        raise RuntimeError(f"Accepted only {len(accepted_rows)} official reverse points, expected {TARGET_COUNT}")

    forward_lines: list[str] = []
    reverse_lines: list[str] = []
    plot_points: list[tuple[float, float]] = []

    for offset in range(0, len(accepted_rows), BATCH_SIZE):
        batch = accepted_rows[offset : offset + BATCH_SIZE]
        forward_payload = [
            {
                "pointNumber": row["pointNumber"],
                "x": row["x"],
                "y": row["y"],
                "h": row["eov_h"],
                "remark": "",
            }
            for row in batch
        ]
        response = post_json(FWD_URL, forward_payload)

        for row, result in zip(batch, response):
            if str(result.get("error")) != "0":
                raise RuntimeError(f"Forward endpoint rejected point {row['pointNumber']}")

            forward_lines.append(
                "\t".join(
                    [
                        row["pointNumber"],
                        f"{row['y']:.3f}",
                        f"{row['x']:.3f}",
                        f"{row['eov_h']:.3f}",
                        f"{float(result['lat']):.10f}",
                        f"{float(result['lon']):.10f}",
                        f"{float(result['h']):.3f}",
                    ]
                )
            )
            reverse_lines.append(
                "\t".join(
                    [
                        row["pointNumber"],
                        f"{row['lat']:.10f}",
                        f"{row['lon']:.10f}",
                        f"{row['h']:.3f}",
                        f"{row['y']:.3f}",
                        f"{row['x']:.3f}",
                        f"{row['eov_h']:.3f}",
                    ]
                )
            )
            plot_points.append((row["lon"], row["lat"]))

    return forward_lines, reverse_lines, plot_points


def write_fixture(path: Path, header: Iterable[str], rows: Iterable[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    content = list(header) + list(rows)
    path.write_text("\n".join(content) + "\n", encoding="utf-8")


def load_grid_points(path: Path) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    with path.open("r", encoding="utf-8") as handle:
        for raw_line in handle:
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            if len(parts) < 2:
                continue
            points.append((float(parts[1]), float(parts[0])))
    return points


def grid_bbox(points: list[tuple[float, float]]) -> tuple[float, float, float, float]:
    lons = [lon for lon, _ in points]
    lats = [lat for _, lat in points]
    return min(lons), min(lats), max(lons), max(lats)


def all_vertices_inside_bbox(polygon: HungaryPolygon, bbox: tuple[float, float, float, float]) -> bool:
    min_lon, min_lat, max_lon, max_lat = bbox
    for xs, ys in polygon.outline:
        for lon, lat in zip(xs, ys):
            if not (min_lon <= lon <= max_lon and min_lat <= lat <= max_lat):
                return False
    return True


def coverage_ratio(points: list[tuple[float, float]], bbox: tuple[float, float, float, float]) -> float:
    min_lon, min_lat, max_lon, max_lat = bbox
    covered = sum(1 for lon, lat in points if min_lon <= lon <= max_lon and min_lat <= lat <= max_lat)
    return covered / len(points)


def plot_points(polygon: HungaryPolygon, points: list[tuple[float, float]]) -> None:
    fig, ax = plt.subplots(figsize=(10, 10), dpi=160)

    for xs, ys in polygon.outline:
        ax.plot(xs, ys, color="#666666", linewidth=1.0, alpha=0.8)

    lons = [lon for lon, _ in points]
    lats = [lat for _, lat in points]
    ax.scatter(lons, lats, s=8, c="#d1495b", alpha=0.75, edgecolors="none", label=f"Benchmark points ({len(points)})")

    ax.set_title("Nationwide Official Benchmark Points Inside Hungary")
    ax.set_xlabel("Longitude")
    ax.set_ylabel("Latitude")
    ax.legend(loc="lower left")
    ax.grid(True, alpha=0.25, linestyle="--")
    ax.set_aspect("equal", adjustable="box")

    min_lon, min_lat, max_lon, max_lat = polygon.bbox
    ax.set_xlim(min_lon - 0.2, max_lon + 0.2)
    ax.set_ylim(min_lat - 0.2, max_lat + 0.2)

    fig.tight_layout()
    fig.savefig(POINTS_PLOT_PATH, bbox_inches="tight")
    plt.close(fig)


def plot_grid_coverage(
    polygon: HungaryPolygon,
    benchmark_points: list[tuple[float, float]],
    hd72_points: list[tuple[float, float]],
    geoid_points: list[tuple[float, float]],
) -> None:
    fig, ax = plt.subplots(figsize=(11, 10), dpi=180)

    for xs, ys in polygon.outline:
        ax.plot(xs, ys, color="black", linewidth=1.1, alpha=0.85, label="Hungary boundary")
        break
    for xs, ys in polygon.outline[1:]:
        ax.plot(xs, ys, color="black", linewidth=1.1, alpha=0.85)

    hd_lons = [lon for lon, _ in hd72_points]
    hd_lats = [lat for _, lat in hd72_points]
    geoid_lons = [lon for lon, _ in geoid_points]
    geoid_lats = [lat for _, lat in geoid_points]
    bench_lons = [lon for lon, _ in benchmark_points]
    bench_lats = [lat for _, lat in benchmark_points]

    ax.scatter(hd_lons, hd_lats, s=2, c="#1f77b4", alpha=0.35, label=f"HD72 correction grid ({len(hd72_points):,})")
    ax.scatter(geoid_lons, geoid_lats, s=2, c="#2a9d8f", alpha=0.25, label=f"Geoid grid ({len(geoid_points):,})")
    ax.scatter(bench_lons, bench_lats, s=8, c="#d1495b", alpha=0.65, edgecolors="none", label=f"Benchmark points ({len(benchmark_points)})")

    ax.set_title("Hungary Boundary, Benchmark Points, and Correction Grid Coverage")
    ax.set_xlabel("Longitude")
    ax.set_ylabel("Latitude")
    ax.grid(True, alpha=0.2, linestyle="--")
    ax.legend(loc="lower left")
    ax.set_aspect("equal", adjustable="box")

    min_lon, min_lat, max_lon, max_lat = polygon.bbox
    ax.set_xlim(min_lon - 0.2, max_lon + 0.2)
    ax.set_ylim(min_lat - 0.2, max_lat + 0.2)

    hd_bbox = grid_bbox(hd72_points)
    geoid_bbox = grid_bbox(geoid_points)
    ax.text(
        0.02,
        0.98,
        "\n".join(
            [
                f"HD72 bbox: lon {hd_bbox[0]:.4f}..{hd_bbox[2]:.4f}, lat {hd_bbox[1]:.4f}..{hd_bbox[3]:.4f}",
                f"Geoid bbox: lon {geoid_bbox[0]:.4f}..{geoid_bbox[2]:.4f}, lat {geoid_bbox[1]:.4f}..{geoid_bbox[3]:.4f}",
            ]
        ),
        transform=ax.transAxes,
        fontsize=8,
        va="top",
        bbox={"boxstyle": "round", "facecolor": "white", "alpha": 0.85},
    )

    fig.tight_layout()
    fig.savefig(GRID_PLOT_PATH, bbox_inches="tight")
    plt.close(fig)


def main() -> None:
    polygon = load_hungary_polygon(BOUNDARY_PATH)
    candidates = sample_points_inside_hungary(polygon, TARGET_COUNT, ROWS, COLS, SEED)
    forward_lines, reverse_lines, benchmark_points = generate_reference_points(candidates)

    header = [
        "// Generated from the official EHT API using points strictly inside Hungary boundary",
        f"// Seed={SEED}, target={TARGET_COUNT}, sampling=stratified-inside-polygon ({ROWS}x{COLS})",
        f"// Boundary source: {BOUNDARY_PATH.name}",
        "// Forward file columns: pointNumber, eovY, eovX, eovH, etrs89Lat, etrs89Lon, etrs89H",
        "// Reverse file columns: pointNumber, etrs89Lat, etrs89Lon, etrs89H, eovY, eovX, eovH",
    ]

    write_fixture(FORWARD_PATH, header, forward_lines)
    write_fixture(REVERSE_PATH, header, reverse_lines)

    hd72_points = load_grid_points(HD72_GRID_PATH)
    geoid_points = load_grid_points(GEOID_GRID_PATH)
    hd72_bbox = grid_bbox(hd72_points)
    geoid_bbox = grid_bbox(geoid_points)

    plot_points(polygon, benchmark_points)
    plot_grid_coverage(polygon, benchmark_points, hd72_points, geoid_points)

    print(f"Wrote forward fixture: {FORWARD_PATH}")
    print(f"Wrote reverse fixture: {REVERSE_PATH}")
    print(f"Saved benchmark point plot: {POINTS_PLOT_PATH}")
    print(f"Saved grid coverage plot: {GRID_PLOT_PATH}")
    print(f"HD72 grid bbox covers all Hungary boundary vertices: {all_vertices_inside_bbox(polygon, hd72_bbox)}")
    print(f"Geoid grid bbox covers all Hungary boundary vertices: {all_vertices_inside_bbox(polygon, geoid_bbox)}")
    print(f"HD72 grid bbox covers benchmark points: {coverage_ratio(benchmark_points, hd72_bbox) * 100:.2f}%")
    print(f"Geoid grid bbox covers benchmark points: {coverage_ratio(benchmark_points, geoid_bbox) * 100:.2f}%")


if __name__ == "__main__":
    main()
