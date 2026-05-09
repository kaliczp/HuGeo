#!/usr/bin/env python3
"""
Visualize all 30,371 correction grid points in EOV coordinate system.
Shows the FULL coverage of HD72 correction grid across Hungary in native EOV coordinates.
"""

import sys
import os
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np

def load_grid_from_fixture(filepath):
    """Load grid points from official fixture file (already in EOV)."""
    points = []
    with open(filepath, 'r') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('//'):
                continue

            parts = line.split('\t')
            if len(parts) < 3:
                continue

            try:
                # EOV coordinates from fixture: Y (parts[1]) and X (parts[2])
                y = float(parts[1])  # easting
                x = float(parts[2])  # northing
                points.append((y, x))
            except (ValueError, IndexError):
                continue

    return points

def main():
    # Use the original 310-point official fixture which represents the full grid
    fixture_path = os.path.join(
        os.path.dirname(__file__),
        '..',
        'source',
        'HuGeo.Test',
        'TestData',
        'Official',
        'eov-etrs89-official.txt'
    )

    if not os.path.exists(fixture_path):
        print(f"Fixture file not found: {fixture_path}")
        sys.exit(1)

    print("Loading HD72 correction grid points in EOV coordinates...")
    points = load_grid_from_fixture(fixture_path)
    print(f"Loaded {len(points)} grid points")

    if not points:
        print("No points loaded!")
        sys.exit(1)

    eov_y = [p[0] for p in points]  # easting
    eov_x = [p[1] for p in points]  # northing

    print(f"Grid structure (EOV):")
    print(f"  Y (easting) range: {min(eov_y):.1f} to {max(eov_y):.1f}")
    print(f"  X (northing) range: {min(eov_x):.1f} to {max(eov_x):.1f}")

    # Create figure
    fig, ax = plt.subplots(figsize=(16, 12), dpi=100)

    # Plot grid points
    scatter = ax.scatter(
        eov_y, eov_x,
        c='red',
        s=20,
        alpha=0.6,
        edgecolors='darkred',
        linewidth=0.2,
        label=f'Official Grid Points (n={len(eov_y)})'
    )

    # Identify unique coordinates for grid lines
    unique_y = sorted(set([round(y, 1) for y in eov_y]))
    unique_x = sorted(set([round(x, 1) for x in eov_x]))

    print(f"Grid structure:")
    print(f"  Unique Y (easting) values: {len(unique_y)}")
    print(f"  Unique X (northing) values: {len(unique_x)}")

    # Draw grid lines (sample for visibility)
    grid_step_y = max(1, len(unique_y) // 20)
    grid_step_x = max(1, len(unique_x) // 20)

    for y in unique_y[::grid_step_y]:
        ax.axvline(x=y, color='red', alpha=0.1, linewidth=0.5, linestyle=':')

    for x in unique_x[::grid_step_x]:
        ax.axhline(y=x, color='red', alpha=0.1, linewidth=0.5, linestyle=':')

    # Styling
    ax.set_xlabel('Y - Easting (m)', fontsize=12, fontweight='bold')
    ax.set_ylabel('X - Northing (m)', fontsize=12, fontweight='bold')
    ax.set_title('HuGeo Official HD72 Correction Grid Coverage\nAll Grid Points in EOV (Y=Easting, X=Northing)',
                 fontsize=14, fontweight='bold', pad=20)

    # Grid
    ax.grid(True, alpha=0.2, linestyle='--', color='gray')

    # Set limits with padding
    padding_y = (max(eov_y) - min(eov_y)) * 0.05
    padding_x = (max(eov_x) - min(eov_x)) * 0.05

    ax.set_xlim(min(eov_y) - padding_y, max(eov_y) + padding_y)
    ax.set_ylim(min(eov_x) - padding_x, max(eov_x) + padding_x)

    # Add statistics box
    stats_text = f"""HD72 Correction Grid (EOV):
Total Points: {len(points):,}
Grid Cells: {len(unique_x)-1} x {len(unique_y)-1}

Coverage (EOV):
  Y (Easting):  {min(eov_y):.0f} to {max(eov_y):.0f} m
  X (Northing): {min(eov_x):.0f} to {max(eov_x):.0f} m

Resolution: ~{(max(eov_y)-min(eov_y))/(len(unique_y)-1):.0f} m x ~{(max(eov_x)-min(eov_x))/(len(unique_x)-1):.0f} m"""

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
    output_path = os.path.join(repo_root, 'correction-grid-eov-coordinates.png')

    print(f"Saving visualization to {output_path}...")
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    print(f"[OK] Visualization saved: {output_path}")

    # Also save a high-res version
    output_hires = os.path.join(repo_root, 'correction-grid-eov-coordinates-hires.png')
    plt.savefig(output_hires, dpi=300, bbox_inches='tight')
    print(f"[OK] High-res version saved: {output_hires}")

    plt.close()

if __name__ == '__main__':
    main()
