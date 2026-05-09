#!/usr/bin/env python3
"""
Visualize actual correction grid points from embedded resources.
Shows the FULL coverage of HD72 correction grid across Hungary.
"""

import sys
import os
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np

def load_grid_points(filepath):
    """Load grid points from CSV file."""
    points = []
    with open(filepath, 'r') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('#'):
                continue

            parts = line.split('\t')
            if len(parts) < 2:
                continue

            try:
                lat = float(parts[0])
                lon = float(parts[1])
                points.append((lon, lat))
            except (ValueError, IndexError):
                continue

    return points

def main():
    grid_path = os.path.join(
        os.path.dirname(__file__),
        '..',
        'source',
        'HuGeo',
        'Resources',
        'Resources',
        'hu_bme_hd72corr.csv'
    )

    if not os.path.exists(grid_path):
        print(f"Grid file not found: {grid_path}")
        sys.exit(1)

    print("Loading HD72 correction grid points...")
    points = load_grid_points(grid_path)
    print(f"Loaded {len(points)} grid points")

    if not points:
        print("No points loaded!")
        sys.exit(1)

    lons = [p[0] for p in points]
    lats = [p[1] for p in points]

    # Create figure
    fig, ax = plt.subplots(figsize=(16, 12), dpi=100)

    # Plot grid points
    scatter = ax.scatter(
        lons, lats,
        c='red',
        s=15,
        alpha=0.6,
        edgecolors='darkred',
        linewidth=0.2,
        label=f'HD72 Correction Grid Points (n={len(lons)})'
    )

    # Add grid cell borders (every Nth point for visibility)
    # Identify unique latitudes and longitudes
    unique_lats = sorted(set(lats))
    unique_lons = sorted(set(lons))

    print(f"Grid structure:")
    print(f"  Unique latitudes: {len(unique_lats)}")
    print(f"  Unique longitudes: {len(unique_lons)}")
    print(f"  Latitude range: {min(lats):.6f} to {max(lats):.6f}")
    print(f"  Longitude range: {min(lons):.6f} to {max(lons):.6f}")

    # Draw grid lines (sample every Nth line for clarity)
    grid_step = max(1, len(unique_lats) // 15)

    for lat in unique_lats[::grid_step]:
        ax.axhline(y=lat, color='red', alpha=0.1, linewidth=0.5, linestyle=':')

    for lon in unique_lons[::grid_step]:
        ax.axvline(x=lon, color='red', alpha=0.1, linewidth=0.5, linestyle=':')

    # Styling
    ax.set_xlabel('Longitude (°E)', fontsize=12, fontweight='bold')
    ax.set_ylabel('Latitude (°N)', fontsize=12, fontweight='bold')
    ax.set_title('HuGeo Official HD72 Correction Grid Coverage\nAll Grid Points Across Hungary',
                 fontsize=14, fontweight='bold', pad=20)

    # Grid
    ax.grid(True, alpha=0.2, linestyle='--', color='gray')

    # Set limits with padding
    padding_lat = (max(lats) - min(lats)) * 0.05
    padding_lon = (max(lons) - min(lons)) * 0.05

    ax.set_xlim(min(lons) - padding_lon, max(lons) + padding_lon)
    ax.set_ylim(min(lats) - padding_lat, max(lats) + padding_lat)

    # Add statistics box
    stats_text = f"""HD72 Correction Grid:
Total Points: {len(points):,}
Grid Cells: {len(unique_lats)-1} x {len(unique_lons)-1}

Coverage:
  Latitude:  {min(lats):.6f}° to {max(lats):.6f}°
  Longitude: {min(lons):.6f}° to {max(lons):.6f}°

Resolution: ~{(max(lats)-min(lats))/(len(unique_lats)-1)*111:.1f} km x ~{(max(lons)-min(lons))/(len(unique_lons)-1)*111*np.cos(np.radians((max(lats)+min(lats))/2)):.1f} km"""

    ax.text(0.02, 0.98, stats_text,
            transform=ax.transAxes,
            fontsize=9,
            verticalalignment='top',
            bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.9),
            family='monospace')

    # Legend
    ax.legend(loc='upper right', fontsize=10, framealpha=0.9)

    # Make axes equal aspect
    ax.set_aspect('equal', adjustable='box')

    plt.tight_layout()

    # Save figure
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    output_path = os.path.join(repo_root, 'correction-grid-points.png')

    print(f"Saving visualization to {output_path}...")
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    print(f"[OK] Visualization saved: {output_path}")

    output_hires = os.path.join(repo_root, 'correction-grid-points-hires.png')
    plt.savefig(output_hires, dpi=300, bbox_inches='tight')
    print(f"[OK] High-res version saved: {output_hires}")

    plt.close()

if __name__ == '__main__':
    main()
