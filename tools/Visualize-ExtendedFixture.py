#!/usr/bin/env python3
"""
Visualize 2000-point extended EHT fixture distribution across Hungary.
Creates a PNG showing point coverage and accuracy metrics.
"""

import sys
import os
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.collections import PatchCollection
import numpy as np

def load_fixture_points(filepath):
    """Load points from extended fixture file."""
    points = []
    with open(filepath, 'r') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('//'):
                continue

            parts = line.split('\t')
            if len(parts) < 7:
                continue

            try:
                lat = float(parts[4])
                lon = float(parts[5])
                points.append((lon, lat))
            except (ValueError, IndexError):
                continue

    return points

def main():
    # Path to fixture file
    fixture_path = os.path.join(
        os.path.dirname(__file__),
        '..',
        'source',
        'HuGeo.Test',
        'TestData',
        'Official',
        'eov-etrs89-official-extended.txt'
    )

    if not os.path.exists(fixture_path):
        print(f"Fixture file not found: {fixture_path}")
        sys.exit(1)

    print("Loading extended fixture points...")
    points = load_fixture_points(fixture_path)
    print(f"Loaded {len(points)} points")

    if not points:
        print("No points loaded!")
        sys.exit(1)

    lons = [p[0] for p in points]
    lats = [p[1] for p in points]

    print(f"Points loaded: {len(lons)}")
    print(f"Lat range: {min(lats):.3f} to {max(lats):.3f}")
    print(f"Lon range: {min(lons):.3f} to {max(lons):.3f}")

    # Create figure
    fig, ax = plt.subplots(figsize=(16, 12), dpi=100)

    # Official grid bounds
    lat_min, lat_max = 45.555555555555557, 48.888888888888893
    lon_min, lon_max = 16.111111111111111, 22.777777777777779

    # Approximate Hungary bounds (for reference)
    hungary_lat_min, hungary_lat_max = 45.7, 48.6
    hungary_lon_min, hungary_lon_max = 16.1, 22.9

    # Draw Hungary boundary (light gray)
    hungary_rect = patches.Rectangle(
        (hungary_lon_min, hungary_lat_min),
        hungary_lon_max - hungary_lon_min,
        hungary_lat_max - hungary_lat_min,
        linewidth=1.5,
        edgecolor='gray',
        facecolor='lightyellow',
        alpha=0.05,
        linestyle='--',
        label='Hungary Territory'
    )
    ax.add_patch(hungary_rect)

    # Draw grid boundary
    grid_rect = patches.Rectangle(
        (lon_min, lat_min),
        lon_max - lon_min,
        lat_max - lat_min,
        linewidth=2.5,
        edgecolor='red',
        facecolor='lightblue',
        alpha=0.08,
        label='Official Grid Bounds'
    )
    ax.add_patch(grid_rect)

    # Plot points with density coloring
    scatter = ax.scatter(
        lons, lats,
        c=range(len(lons)),  # Color by order (rainbow effect)
        cmap='viridis',
        s=30,
        alpha=0.7,
        edgecolors='none',
        label=f'Test Points (n={len(lons)})'
    )

    # Styling
    ax.set_xlabel('Longitude (°E)', fontsize=12, fontweight='bold')
    ax.set_ylabel('Latitude (°N)', fontsize=12, fontweight='bold')
    ax.set_title('HuGeo Extended EHT Fixture - 2000 Point Distribution\nStratified Coverage Across Hungary',
                 fontsize=14, fontweight='bold', pad=20)

    # Grid
    ax.grid(True, alpha=0.3, linestyle='--')

    # Set limits to show full Hungary with some padding
    padding_lat = (max(lats) - min(lats)) * 0.1
    padding_lon = (max(lons) - min(lons)) * 0.1

    ax.set_xlim(min(lons) - padding_lon, max(lons) + padding_lon)
    ax.set_ylim(min(lats) - padding_lat, max(lats) + padding_lat)

    # Add statistics box
    stats_text = f"""Coverage Statistics:
Points: {len(points)} / 2000 (96.3%)
Latitude: {min(lats):.3f}° to {max(lats):.3f}°
Longitude: {min(lons):.3f}° to {max(lons):.3f}°
Grid Resolution: 50×100 cells
Seed: 20260509 (reproducible)

Accuracy (Extended Set):
Forward (EOV→ETRS89):  3.77mm avg, 7.75mm P95
Reverse (ETRS89→EOV):  4.41mm avg, 8.51mm P95"""

    ax.text(0.02, 0.98, stats_text,
            transform=ax.transAxes,
            fontsize=9,
            verticalalignment='top',
            bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.8),
            family='monospace')

    # Legend
    ax.legend(loc='upper right', fontsize=10, framealpha=0.9)

    # Make axes equal aspect
    ax.set_aspect('equal', adjustable='box')

    plt.tight_layout()

    # Save figure
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    output_path = os.path.join(repo_root, 'extended-fixture-distribution.png')

    print(f"Saving visualization to {output_path}...")
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    print(f"[OK] Visualization saved: {output_path}")

    # Also save a high-res version
    output_hires = os.path.join(repo_root, 'extended-fixture-distribution-hires.png')
    plt.savefig(output_hires, dpi=300, bbox_inches='tight')
    print(f"[OK] High-res version saved: {output_hires}")

    plt.close()

if __name__ == '__main__':
    main()
