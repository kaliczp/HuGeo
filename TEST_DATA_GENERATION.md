# Extended Test Data Generation

## 📊 Kiterjesztett Teszt Fixture-ök Generálása

Az **Generate-ExtendedEhtFixtures.ps1** script nagyobb tesztadathalmazokat generál az EHT API-ból, az official grid határainak stratifikált mintavételezésével.

---

## 🎯 Miért?

### Jelenlegi Teszt (310 pont)
```
Paraméterek:
  - Grid: 20×40 cella
  - Pontok: 310
  - Lefedettség: Jó, de ritka
  - Boundary-teszt: Nincs explicit
```

### Kiterjesztett Teszt (2000 pont)
```
Paraméterek:
  - Grid: 50×100 cella (2x-es rezolúció)
  - Pontok: 2000 (6.5× több)
  - Lefedettség: Kiváló, sűrű
  - Boundary-teszt: Grid határait explicit használja
```

---

## 📍 Grid Határok (Official Correction Grid)

Az **hu_bme_hd72corr.csv** fájlból extrahálva:

```
Latitude:  45.555555555555557 ... 48.888888888888893  (Δ ~3.333°)
Longitude: 16.111111111111111 ... 22.777777777777779  (Δ ~6.667°)

Grid resolution: ~0.0278° (2.8 perc)
```

Ezek az **official grid** határai, amelyek biztosítják hogy minden generált pont **covered** a javító gridekben.

---

## 🚀 Futtatás

### Alapértelmezett (2000 pont)

```powershell
cd tools
.\Generate-ExtendedEhtFixtures.ps1
```

### Egyedi paraméterek

```powershell
# 5000 pont, 100×100 grid (súper felbontás)
.\Generate-ExtendedEhtFixtures.ps1 -TargetCount 5000 -Rows 100 -Cols 100

# 1000 pont, egyedi seed
.\Generate-ExtendedEhtFixtures.ps1 -TargetCount 1000 -Seed 12345

# Egyedi output könyvtár
.\Generate-ExtendedEhtFixtures.ps1 -OutputDirectory "C:\MyTests"
```

### Paraméter leírás

| Paraméter | Default | Leírás |
|-----------|---------|--------|
| `TargetCount` | 2000 | Célszám pont |
| `Rows` | 50 | Grid sorok (latitude) |
| `Cols` | 100 | Grid oszlopok (longitude) |
| `Seed` | 20260509 | Random seed (reprodukálható) |
| `LatMin` ... `LatMax` | 45.556...48.889 | Grid határok (official) |
| `LonMin` ... `LonMax` | 16.111...22.778 | Grid határok (official) |
| `OutputSuffix` | "-extended" | Output fájl suffix |

---

## 📤 Output

A script **két fájlt** generál:

### Forward (EOV → ETRS89)

Fájl: `eov-etrs89-official-extended.txt`

```
// Generated from https://eht.gnssnet.hu/kezi-bevitel
// Seed=20260509, stratified random sample across official grid bbox (50x100)
// Grid bounds: lat [45.555555555555557..48.888888888888893], lon [16.111111111111111..22.777777777777779]
// Extended test fixture: 2000 points (previously 310)
E00001	586883.573	37092.652	283.843	45.6758297693	18.2380862119	328.505
E00002	550802.098	50602.219	193.618	45.7928822535	17.7718630739	238.354
...
```

### Reverse (ETRS89 → EOV)

Fájl: `etrs89-eov-official-extended.txt`

```
// Generated from https://eht.gnssnet.hu/kezi-bevitel
// ...
E00001	45.6758297693	18.2380862119	328.505	586883.573	37092.652	283.843
E00002	45.7928822535	17.7718630739	238.354	550802.098	50602.219	193.618
...
```

---

## ✅ Mit Validál Ez?

### Stratifikációs Lefedettség
- ✅ 50×100 cella = 5000 potenciális minta
- ✅ Mindegyik cellából 1 pont (random)
- ✅ Egész Magyarország egyenletesen lefedett

### Grid Határok
- ✅ Explicit `LatMin`/`LatMax` és `LonMin`/`LonMax` az official gridből
- ✅ Biztosítja hogy 100% pont az official grid-ben van
- ✅ Nincs "uncovered point" szürprice

### Pontosság Validáció
2000 pont = mélyebb statisztika:
- P50, P95, P99 percentilisek
- Outlier-ek (edge cases)
- Boundary behavior
- Szystematikus hiba detekció

---

## 🔗 Integráció Teszt Suite-ba

Az extended fixture-öket integrálni lehet:

```csharp
[Fact]
public void OfficialGrid_ExtendedFixture_ValidatesFullCoverage()
{
    var points = LoadExtendedOfficialPoints();  // 2000 pont
    Assert.True(points.Count >= 1500, "Extended fixture should have many points");

    var errors = new List<double>();
    
    foreach (var pt in points)
    {
        var hd72 = new Hd72Coordinate(pt.EovY, pt.EovX, pt.EovH);
        var etrs89 = transformer.TransformHd72ToEtrs89(hd72);
        
        errors.Add(HaversineMeters(etrs89.Latitude, etrs89.Longitude, 
                                   pt.ExpectedLat, pt.ExpectedLon));
    }

    // Statisztika
    Assert.True(errors.Average() < 0.005, "Mean error < 5mm");
    Assert.True(Percentile(errors, 0.95) < 0.020, "P95 error < 2cm");
    Assert.True(errors.Max() < 0.050, "Max error < 5cm on extended set");
}
```

---

## ⚡ Teljesítmény

**Futási idő** (körülbelül):
- 2000 pont: ~2-5 perc (EHT API lekérdezések)
- 5000 pont: ~5-10 perc
- Függ az EHT API válaszidejétől

**Internet szükséges**: Igen, az EHT web API-hoz való kapcsolat

---

## 🛠️ Troubleshooting

### "Point outside coverage"
→ Valószínűleg API hiba, retry

### "Forward endpoint rejected point"
→ Legalább 1-2 pont lehet uncovered, ez OK

### API lassúnak tűnik
→ Batch méret csökkentése ajánlott: `-batchSize 10`

---

## 📚 Referencia

- **EHT Transformation API**: https://eht.gnssnet.hu/api/transformation/
- **Official Grid**: `hu_bme_hd72corr.csv` (beágyazva)
- **Grid Resolution**: ~0.0278° (~3.1 km)

