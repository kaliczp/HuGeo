param(
    [int]$Seed = 20260502,
    [int]$TargetCount = 320,
    [int]$Rows = 20,
    [int]$Cols = 40,
    [double]$LatMin = 45.55,
    [double]$LatMax = 48.65,
    [double]$LonMin = 15.90,
    [double]$LonMax = 22.95,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\source\HuGeo.Test\TestData\Official")
)

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

Write-Host "Generating official EHT fixtures..."
Write-Host "Seed: $Seed"
Write-Host "TargetCount: $TargetCount"
Write-Host "BBox: lat $LatMin..$LatMax, lon $LonMin..$LonMax"

$candidates = New-StratifiedCandidates -RowCount $Rows -ColumnCount $Cols
$accepted = New-Object System.Collections.Generic.List[object]

for ($offset = 0; $offset -lt $candidates.Count; $offset += $batchSize) {
    $batch = $candidates[$offset..([math]::Min($offset + $batchSize - 1, $candidates.Count - 1))]
    $request = for ($i = 0; $i -lt $batch.Count; $i++) {
        $index = $offset + $i + 1
        [pscustomobject]@{
            pointNumber = ('P{0:D4}' -f $index)
            lat = $batch[$i].Latitude
            lon = $batch[$i].Longitude
            h = $batch[$i].Height
            remark = ''
        }
    }

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

    if ($accepted.Count -ge $TargetCount) {
        break
    }
}

if ($accepted.Count -lt $TargetCount) {
    while ($accepted.Count -lt $TargetCount) {
        $fallback = [pscustomobject]@{
            Latitude  = $rng.NextDouble() * ($LatMax - $LatMin) + $LatMin
            Longitude = $rng.NextDouble() * ($LonMax - $LonMin) + $LonMin
            Height    = [math]::Round($rng.NextDouble() * 500.0, 3)
        }

        $request = @([pscustomobject]@{
            pointNumber = ('P{0:D4}' -f ($accepted.Count + 1))
            lat = $fallback.Latitude
            lon = $fallback.Longitude
            h = $fallback.Height
            remark = ''
        })

        $response = Invoke-EhtJsonPost -Url $revUrl -Payload $request
        $item = $response[0]
        if ([string]$item.error -ne '0') {
            continue
        }
        $accepted.Add([pscustomobject]@{
            PointNumber = $request[0].pointNumber
            Lat         = [double]$request[0].lat
            Lon         = [double]$request[0].lon
            Height      = [double]$request[0].h
            Y           = [double]$item.y
            X           = [double]$item.x
            EovHeight   = [double]$item.h
        })
    }
}

$accepted = @($accepted | Select-Object -First $TargetCount)

$forwardLines = New-Object System.Collections.Generic.List[string]
$reverseLines = New-Object System.Collections.Generic.List[string]

for ($offset = 0; $offset -lt $accepted.Count; $offset += $batchSize) {
    $batch = $accepted[$offset..([math]::Min($offset + $batchSize - 1, $accepted.Count - 1))]
    foreach ($point in $batch) {
        $forwardRequest = @([pscustomobject]@{
            pointNumber = $point.PointNumber
            x = $point.Y
            y = $point.X
            h = $point.EovHeight
            remark = ''
        })

        $forwardResponse = Invoke-EhtJsonPost -Url $fwdUrl -Payload $forwardRequest
        if ([string]$forwardResponse[0].error -ne '0') {
            throw "Forward endpoint rejected point $($point.PointNumber)."
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
}

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
    "// Seed=$Seed, stratified random sample across Hungary bbox (${Rows}x${Cols})"
)

$forwardPath = Join-Path $OutputDirectory 'eov-etrs89-official.txt'
$reversePath = Join-Path $OutputDirectory 'etrs89-eov-official.txt'

Write-FixtureFile -Path $forwardPath -Header $header -Lines $forwardLines.ToArray()
Write-FixtureFile -Path $reversePath -Header $header -Lines $reverseLines.ToArray()

Write-Host "Wrote $($accepted.Count) rows to $forwardPath"
Write-Host "Wrote $($accepted.Count) rows to $reversePath"
