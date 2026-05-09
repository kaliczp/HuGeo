param(
    [int]$Seed = 20260509,
    [int]$TargetCount = 2000,
    [int]$Rows = 50,
    [int]$Cols = 100,
    # Official grid bounds (hu_bme_hd72corr.csv extent)
    [double]$LatMin = 45.555555555555557,
    [double]$LatMax = 48.888888888888893,
    [double]$LonMin = 16.111111111111111,
    [double]$LonMax = 22.777777777777779,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\source\HuGeo.Test\TestData\Official"),
    [string]$OutputSuffix = "-extended"
)

<#
.DESCRIPTION
Generates extended EHT test fixtures with higher resolution coverage of the official grid.
Uses stratified random sampling to cover Hungary uniformly across the official grid bounds.

.PARAMETER TargetCount
Number of test points to generate (default: 2000, previously: 310)

.PARAMETER Rows, Cols
Grid resolution for stratification (default: 50×100 cells)

.PARAMETER LatMin, LatMax, LonMin, LonMax
Official grid boundaries (extracted from hu_bme_hd72corr.csv)
#>

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Net.Http

$revUrl = 'https://eht.gnssnet.hu/api/transformation/etrs89-to-eov'
$fwdUrl = 'https://eht.gnssnet.hu/api/transformation/eov-to-etrs89'
$batchSize = 20
$rng = [System.Random]::new($Seed)
$client = [System.Net.Http.HttpClient]::new()

function Invoke-EhtJsonPost {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][object[]]$Payload
    )

    $json = To-JsonArray -Items $Payload
    $content = [System.Net.Http.StringContent]::new(
        $json,
        [System.Text.Encoding]::UTF8,
        'application/json'
    )

    $response = $client.PostAsync($Url, $content).GetAwaiter().GetResult()
    $response.EnsureSuccessStatusCode() | Out-Null
    $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return $raw | ConvertFrom-Json
}

function Escape-JsonString {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value)

    return $Value.
        Replace('\', '\\').
        Replace('"', '\"').
        Replace("`r", '\r').
        Replace("`n", '\n').
        Replace("`t", '\t')
}

function Format-JsonValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return 'null'
    }

    if ($Value -is [string]) {
        return '"' + (Escape-JsonString -Value $Value) + '"'
    }

    if ($Value -is [bool]) {
        return $(if ($Value) { 'true' } else { 'false' })
    }

    if ($Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal] -or $Value -is [float]) {
        return ([System.Convert]::ToString($Value, [System.Globalization.CultureInfo]::InvariantCulture))
    }

    return '"' + (Escape-JsonString -Value ([string]$Value)) + '"'
}

function To-JsonObject {
    param([Parameter(Mandatory = $true)][object]$Item)

    $parts = foreach ($prop in $Item.PSObject.Properties) {
        '"' + $prop.Name + '":' + (Format-JsonValue -Value $prop.Value)
    }

    return '{' + ($parts -join ',') + '}'
}

function To-JsonArray {
    param([Parameter(Mandatory = $true)][object[]]$Items)

    $normalized = @($Items)
    $parts = foreach ($item in $normalized) {
        To-JsonObject -Item $item
    }

    return '[' + ($parts -join ',') + ']'
}

function New-StratifiedCandidates {
    param(
        [int]$RowCount,
        [int]$ColumnCount
    )

    $items = New-Object System.Collections.Generic.List[object]

    for ($row = 0; $row -lt $RowCount; $row++) {
        $lat0 = $LatMin + (($LatMax - $LatMin) * $row / $RowCount)
        $lat1 = $LatMin + (($LatMax - $LatMin) * ($row + 1) / $RowCount)

        for ($col = 0; $col -lt $ColumnCount; $col++) {
            $lon0 = $LonMin + (($LonMax - $LonMin) * $col / $ColumnCount)
            $lon1 = $LonMin + (($LonMax - $LonMin) * ($col + 1) / $ColumnCount)

            $items.Add([pscustomobject]@{
                Latitude  = $rng.NextDouble() * ($lat1 - $lat0) + $lat0
                Longitude = $rng.NextDouble() * ($lon1 - $lon0) + $lon0
                Height    = [math]::Round($rng.NextDouble() * 500.0, 3)
            })
        }
    }

    return $items
}

function Write-FixtureFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Header,
        [Parameter(Mandatory = $true)][string[]]$Lines
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $content = New-Object System.Collections.Generic.List[string]
    $content.AddRange($Header)
    $content.AddRange($Lines)
    Set-Content -Path $Path -Value $content -Encoding utf8
}

Write-Host "Generating extended official EHT fixtures..."
Write-Host "Seed: $Seed"
Write-Host "TargetCount: $TargetCount (previous: 310)"
Write-Host "Grid Resolution: ${Rows}x${Cols} cells (previous: 20x40)"
Write-Host "Grid BBox: lat $LatMin..$LatMax, lon $LonMin..$LonMax"
Write-Host "Coverage: Hungary within official correction grid bounds"
Write-Host ""

$candidates = New-StratifiedCandidates -RowCount $Rows -ColumnCount $Cols
$accepted = New-Object System.Collections.Generic.List[object]

Write-Host "Querying EHT API for $($candidates.Count) candidate points..."
$progress = 0

for ($offset = 0; $offset -lt $candidates.Count; $offset += $batchSize) {
    $batch = $candidates[$offset..([math]::Min($offset + $batchSize - 1, $candidates.Count - 1))]
    $request = for ($i = 0; $i -lt $batch.Count; $i++) {
        $index = $offset + $i + 1
        [pscustomobject]@{
            pointNumber = ('E{0:D5}' -f $index)  # E=Extended, 5-digit for 2000+ points
            lat = $batch[$i].Latitude
            lon = $batch[$i].Longitude
            h = $batch[$i].Height
            remark = ''
        }
    }

    try {
        $response = Invoke-EhtJsonPost -Url $revUrl -Payload $request
        for ($i = 0; $i -lt $response.Count; $i++) {
            $item = $response[$i]
            if ([string]$item.error -ne '0') {
                continue
            }

            $accepted.Add([pscustomobject]@{
                PointNumber = $request[$i].pointNumber
                Lat         = [double]$request[$i].lat
                Lon         = [double]$request[$i].lon
                Height      = [double]$request[$i].h
                Y           = [double]$item.y
                X           = [double]$item.x
                EovHeight   = [double]$item.h
            })

            if ($accepted.Count -ge $TargetCount) {
                break
            }
        }
    }
    catch {
        Write-Warning "Batch error at offset $offset`: $($_.Exception.Message)"
        continue
    }

    $progress = [int]($offset / $candidates.Count * 100)
    Write-Host "Progress: $progress% ($($accepted.Count)/$TargetCount accepted)" -NoNewline -ForegroundColor Cyan
    Write-Host ""

    if ($accepted.Count -ge $TargetCount) {
        break
    }
}

Write-Host "Reverse lookup complete: $($accepted.Count) accepted points"
Write-Host ""

$accepted = @($accepted | Select-Object -First $TargetCount)

if ($accepted.Count -lt $TargetCount) {
    Write-Warning "Only $($accepted.Count) points accepted, target was $TargetCount"
}

$forwardLines = New-Object System.Collections.Generic.List[string]
$reverseLines = New-Object System.Collections.Generic.List[string]

Write-Host "Generating forward fixtures..."

for ($offset = 0; $offset -lt $accepted.Count; $offset += $batchSize) {
    $batch = $accepted[$offset..([math]::Min($offset + $batchSize - 1, $accepted.Count - 1))]
    foreach ($point in $batch) {
        $forwardRequest = @([pscustomobject]@{
            pointNumber = $point.PointNumber
            x = $point.X
            y = $point.Y
            h = $point.EovHeight
            remark = ''
        })

        try {
            $forwardResponse = Invoke-EhtJsonPost -Url $fwdUrl -Payload $forwardRequest
            if ([string]$forwardResponse[0].error -ne '0') {
                Write-Warning "Forward endpoint rejected point $($point.PointNumber)"
                continue
            }

            $forwardLines.Add((
                "{0}`t{1:F3}`t{2:F3}`t{3:F3}`t{4:F10}`t{5:F10}`t{6:F3}" -f
                $point.PointNumber,
                $point.Y,
                $point.X,
                $point.EovHeight,
                [double]$forwardResponse[0].lat,
                [double]$forwardResponse[0].lon,
                [double]$forwardResponse[0].h
            ))
        }
        catch {
            Write-Warning "Error processing point $($point.PointNumber): $($_.Exception.Message)"
        }
    }

    Write-Host "Forward: $($forwardLines.Count)/$($accepted.Count) lines" -NoNewline -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "Generating reverse fixtures..."

foreach ($point in $accepted) {
    $reverseLines.Add((
        "{0}`t{1:F10}`t{2:F10}`t{3:F3}`t{4:F3}`t{5:F3}`t{6:F3}" -f
        $point.PointNumber,
        $point.Lat,
        $point.Lon,
        $point.Height,
        $point.Y,
        $point.X,
        $point.EovHeight
    ))
}

$header = @(
    '// Generated from https://eht.gnssnet.hu/kezi-bevitel',
    "// Seed=$Seed, stratified random sample across official grid bbox (${Rows}x${Cols})",
    "// Grid bounds: lat [$LatMin..$LatMax], lon [$LonMin..$LonMax]",
    "// Extended test fixture: $TargetCount points (previously 310)"
)

$forwardPath = Join-Path $OutputDirectory "eov-etrs89-official${OutputSuffix}.txt"
$reversePath = Join-Path $OutputDirectory "etrs89-eov-official${OutputSuffix}.txt"

Write-FixtureFile -Path $forwardPath -Header $header -Lines $forwardLines.ToArray()
Write-FixtureFile -Path $reversePath -Header $header -Lines $reverseLines.ToArray()

Write-Host ""
Write-Host "✅ SUCCESS" -ForegroundColor Green
Write-Host "Wrote $($accepted.Count) rows to $forwardPath"
Write-Host "Wrote $($accepted.Count) rows to $reversePath"
Write-Host ""
Write-Host "Usage: Add test using 2000-point fixture for extended coverage validation"
