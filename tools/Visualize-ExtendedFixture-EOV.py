#!/usr/bin/env python3
"""
Visualize 2000-point extended EHT fixture distribution in EOV coordinate system.
Shows points in native EOV (Y=easting, X=northing) coordinates.
"""

import sys
import os
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import numpy as np

def load_fixture_points(filepath):
    """Load points from extended fixture file, extracting EOV coordinates."""
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
                # EOV coordinates: Y=easting, X=northing
                eov_y = float(parts[1])  # Y (easting)
                eov_x = float(parts[2])  # X (northing)
                points.append((eov_y, eov_x))
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

    print("Loading extended fixture points in EOV coordinates...")
    points = load_fixture_points(fixture_path)
    print(f"Loaded {len(points)} points")

    if not points:
        print("No points loaded!")
        sys.exit(1)

    eov_y = [p[0] for p in points]  # easting
    eov_x = [p[1] for p in points]  # northing

    print(f"Points loaded: {len(eov_y)}")
    print(f"Y (easting) range: {min(eov_y):.1f} to {max(eov_y):.1f}")
    print(f"X (northing) range: {min(eov_x):.1f} to {max(eov_x):.1f}")

    # Create figure
    fig, ax = plt.subplots(figsize=(14, 12), dpi=100)

    # Official grid bounds in EOV
    y_min, y_max = 0, 300000  # easting range
    x_min, x_max = 40000, 330000  # northing range

    # Draw grid boundary
    grid_rect = patches.Rectangle(
        (y_min, x_min),
        y_max - y_min,
        x_max - x_min,
        linewidth=2.5,
        edgecolor='red',
        facecolor='lightblue',
        alpha=0.08,
        label='EOV Grid Bounds'
    )
    ax.add_patch(grid_rect)

    # Plot points with color gradient
    scatter = ax.scatter(
        eov_y, eov_x,
        c=range(len(eov_y)),  # Color by order (rainbow effect)
        cmap='viridis',
        s=25,
        alpha=0.7,
        edgecolors='none',
        label=f'Test Points (n={len(eov_y)})'
    )

    # Styling
    ax.set_xlabel('Y - Easting (m)', fontsize=12, fontweight='bold')
    ax.set_ylabel('X - Northing (m)', fontsize=12, fontweight='bold')
    ax.set_title('HuGeo Extended EHT Fixture - EOV Coordinate Distribution\n2000 Points in Native EOV (Y=Easting, X=Northing)',
                 fontsize=14, fontweight='bold', pad=20)

    # Grid
    ax.grid(True, alpha=0.3, linestyle='--')

    # Set limits
    padding_y = (max(eov_y) - min(eov_y)) * 0.1
    padding_x = (max(eov_x) - min(eov_x)) * 0.1

    ax.set_xlim(min(eov_y) - padding_y, max(eov_y) + padding_y)
    ax.set_ylim(min(eov_x) - padding_x, max(eov_x) + padding_x)

    # Add statistics box
    stats_text = f"""EOV Coordinate Distribution:
Points: {len(points)} / 2000
Y (Easting): {min(eov_y):.0f} to {max(eov_y):.0f} m
X (Northing): {min(eov_x):.0f} to {max(eov_x):.0f} m

EOV Grid Bounds:
Y (Easting): 0 to 300,000 m
X (Northing): 40,000 to 330,000 m

Seed: 20260509 (reproducible)
Grid Resolution: 50×100 cells"""

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
    output_path = os.path.join(repo_root, 'extended-fixture-eov-coordinates.png')

    print(f"Saving visualization to {output_path}...")
    plt.savefig(output_path, dpi=150, bbox_inches='tight')
    print(f"[OK] Visualization saved: {output_path}")

    # Also save a high-res version
    output_hires = os.path.join(repo_root, 'extended-fixture-eov-coordinates-hires.png')
    plt.savefig(output_hires, dpi=300, bbox_inches='tight')
    print(f"[OK] High-res version saved: {output_hires}")

    plt.close()

if __name__ == '__main__':
    main()
