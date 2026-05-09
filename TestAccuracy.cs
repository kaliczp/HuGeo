using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HuGeo.Api;
using HuGeo.Core.Coordinates;
using HuGeo.DataAccess.Loaders;
using HuGeo.DataAccess.Repository;

// Standalone script to measure accuracy on official fixtures
class Program
{
    static async Task Main()
    {
        Console.WriteLine("HuGeo Accuracy Analysis on Official Fixtures\n");

        var repo = new GridDataRepository(new EmbeddedResourceGridLoader());
        await repo.InitializeAsync();

        var transformer = new CoordinateTransformer(repo, TransformationMode.OfficialGrid);
        await transformer.InitializeAsync();

        // Test 1: Official EOV → ETRS89 fixture
        await TestOfficialEovToEtrs89(transformer);

        // Test 2: Official ETRS89 → EOV fixture (reverse)
        await TestOfficialEtrs89ToEov(transformer);
    }

    static async Task TestOfficialEovToEtrs89(CoordinateTransformer transformer)
    {
        Console.WriteLine("=== Official EOV → ETRS89 Fixture Test ===");

        var fixturePath = "source/HuGeo.Test/TestData/Official/eov-etrs89-official.txt";
        var lines = File.ReadAllLines(fixturePath)
            .Where(l => !l.StartsWith("//") && !string.IsNullOrWhiteSpace(l))
            .ToList();

        var latErrors = new List<double>();
        var lonErrors = new List<double>();
        var hErrors = new List<double>();
        var totalErrors = new List<double>();
        var successCount = 0;

        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 6) continue;

            if (!double.TryParse(parts[1], out var eovY) ||
                !double.TryParse(parts[2], out var eovX) ||
                !double.TryParse(parts[3], out var eovH) ||
                !double.TryParse(parts[4], out var expectedLat) ||
                !double.TryParse(parts[5], out var expectedLon) ||
                !double.TryParse(parts[6], out var expectedH))
                continue;

            try
            {
                var hd72 = new Hd72Coordinate(eovY, eovX, eovH);
                var etrs89 = transformer.TransformToEtrs89(hd72);

                var latErr = Math.Abs(etrs89.Latitude - expectedLat) * 111320; // meters
                var lonErr = Math.Abs(etrs89.Longitude - expectedLon) * 111320 * Math.Cos(Math.PI * expectedLat / 180); // meters
                var hErr = Math.Abs(etrs89.Height - expectedH);

                latErrors.Add(latErr);
                lonErrors.Add(lonErr);
                hErrors.Add(hErr);
                totalErrors.Add(Math.Sqrt(latErr * latErr + lonErr * lonErr + hErr * hErr));
                successCount++;
            }
            catch
            {
                // Point outside coverage
            }
        }

        if (successCount > 0)
        {
            Console.WriteLine($"Points tested: {successCount}/{lines.Count}");
            Console.WriteLine($"Latitude error:  avg={latErrors.Average():F5} m,  max={latErrors.Max():F5} m,  p95={Percentile(latErrors, 0.95):F5} m");
            Console.WriteLine($"Longitude error: avg={lonErrors.Average():F5} m,  max={lonErrors.Max():F5} m,  p95={Percentile(lonErrors, 0.95):F5} m");
            Console.WriteLine($"Height error:    avg={hErrors.Average():F5} m,  max={hErrors.Max():F5} m,  p95={Percentile(hErrors, 0.95):F5} m");
            Console.WriteLine($"3D RMS error:    avg={totalErrors.Average():F5} m,  max={totalErrors.Max():F5} m,  p95={Percentile(totalErrors, 0.95):F5} m");
            Console.WriteLine();
        }
    }

    static async Task TestOfficialEtrs89ToEov(CoordinateTransformer transformer)
    {
        Console.WriteLine("=== Official ETRS89 → EOV Fixture Test (Reverse) ===");

        var fixturePath = "source/HuGeo.Test/TestData/Official/etrs89-eov-official.txt";
        var lines = File.ReadAllLines(fixturePath)
            .Where(l => !l.StartsWith("//") && !string.IsNullOrWhiteSpace(l))
            .ToList();

        var eastingErrors = new List<double>();
        var northingErrors = new List<double>();
        var hErrors = new List<double>();
        var totalErrors = new List<double>();
        var successCount = 0;

        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 6) continue;

            if (!double.TryParse(parts[1], out var lat) ||
                !double.TryParse(parts[2], out var lon) ||
                !double.TryParse(parts[3], out var h) ||
                !double.TryParse(parts[4], out var expectedY) ||
                !double.TryParse(parts[5], out var expectedX) ||
                !double.TryParse(parts[6], out var expectedH))
                continue;

            try
            {
                var etrs89 = new Etrs89Coordinate(lat, lon, h);
                var hd72 = transformer.TransformToEov(etrs89);

                var eastErr = Math.Abs(hd72.Easting - expectedY);
                var northErr = Math.Abs(hd72.Northing - expectedX);
                var hErr = Math.Abs(hd72.Height - expectedH);

                eastingErrors.Add(eastErr);
                northingErrors.Add(northErr);
                hErrors.Add(hErr);
                totalErrors.Add(Math.Sqrt(eastErr * eastErr + northErr * northErr + hErr * hErr));
                successCount++;
            }
            catch
            {
                // Point outside coverage
            }
        }

        if (successCount > 0)
        {
            Console.WriteLine($"Points tested: {successCount}/{lines.Count}");
            Console.WriteLine($"Easting error:   avg={eastingErrors.Average():F5} m,  max={eastingErrors.Max():F5} m,  p95={Percentile(eastingErrors, 0.95):F5} m");
            Console.WriteLine($"Northing error:  avg={northingErrors.Average():F5} m,  max={northingErrors.Max():F5} m,  p95={Percentile(northingErrors, 0.95):F5} m");
            Console.WriteLine($"Height error:    avg={hErrors.Average():F5} m,  max={hErrors.Max():F5} m,  p95={Percentile(hErrors, 0.95):F5} m");
            Console.WriteLine($"3D RMS error:    avg={totalErrors.Average():F5} m,  max={totalErrors.Max():F5} m,  p95={Percentile(totalErrors, 0.95):F5} m");
            Console.WriteLine();
        }
    }

    static double Percentile(List<double> values, double p)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
