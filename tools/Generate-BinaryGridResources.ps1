param(
    [string]$ResourceDirectory = (Join-Path $PSScriptRoot "..\source\HuGeo\Resources\Resources")
)

$ErrorActionPreference = 'Stop'

function Parse-HuDouble {
    param([Parameter(Mandatory = $true)][string]$Value)
    return [double]::Parse($Value.Trim().Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
}

function Read-GridRecords {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$ValueCount
    )

    $records = [System.Collections.Generic.List[object]]::new()
    foreach ($line in [IO.File]::ReadLines($Path)) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#') -or $trimmed.StartsWith('//')) {
            continue
        }

        $parts = $trimmed.Split("`t", [StringSplitOptions]::RemoveEmptyEntries)
        if ($parts.Length -lt (2 + $ValueCount)) {
            continue
        }

        $values = New-Object double[] $ValueCount
        for ($i = 0; $i -lt $ValueCount; $i++) {
            $values[$i] = Parse-HuDouble $parts[$i + 2]
        }

        $records.Add([pscustomobject]@{
            Lat = Parse-HuDouble $parts[0]
            Lon = Parse-HuDouble $parts[1]
            Values = $values
        })
    }

    if ($records.Count -eq 0) {
        throw "Grid file is empty: $Path"
    }

    return $records
}

function Write-Header {
    param(
        [Parameter(Mandatory = $true)][IO.BinaryWriter]$Writer,
        [Parameter(Mandatory = $true)][int]$Magic,
        [Parameter(Mandatory = $true)][int]$Rows,
        [Parameter(Mandatory = $true)][int]$Cols,
        [Parameter(Mandatory = $true)][double]$Lon0,
        [Parameter(Mandatory = $true)][double]$Lat0,
        [Parameter(Mandatory = $true)][double]$LonStep,
        [Parameter(Mandatory = $true)][double]$LatStep
    )

    $Writer.Write([int]$Magic)
    $Writer.Write([int]1)
    $Writer.Write([int]$Rows)
    $Writer.Write([int]$Cols)
    $Writer.Write([double]$Lon0)
    $Writer.Write([double]$Lat0)
    $Writer.Write([double]$LonStep)
    $Writer.Write([double]$LatStep)
}

function Write-FloatArray {
    param(
        [Parameter(Mandatory = $true)][IO.BinaryWriter]$Writer,
        [Parameter(Mandatory = $true)][double[]]$Values
    )

    foreach ($value in $Values) {
        $Writer.Write([single]$value)
    }
}

function Convert-Hd72Grid {
    param([string]$InputPath, [string]$OutputPath)

    $records = Read-GridRecords -Path $InputPath -ValueCount 2
    $latValues = $records | ForEach-Object Lat | Sort-Object -Descending -Unique
    $lonValues = $records | ForEach-Object Lon | Sort-Object -Unique
    $rows = $latValues.Count
    $cols = $lonValues.Count
    if ($rows * $cols -ne $records.Count) {
        throw "HD72 grid is not rectangular: rows=$rows cols=$cols count=$($records.Count)"
    }

    $latOffsets = New-Object double[] ($rows * $cols)
    $lonOffsets = New-Object double[] ($rows * $cols)
    $byPoint = @{}
    foreach ($record in $records) {
        $byPoint["$($record.Lat)|$($record.Lon)"] = $record.Values
    }

    for ($j = 0; $j -lt $rows; $j++) {
        for ($i = 0; $i -lt $cols; $i++) {
            $key = "$($latValues[$j])|$($lonValues[$i])"
            if (-not $byPoint.ContainsKey($key)) {
                throw "Missing HD72 grid point: $key"
            }

            $idx = $j * $cols + $i
            $latOffsets[$idx] = $byPoint[$key][0]
            $lonOffsets[$idx] = $byPoint[$key][1]
        }
    }

    $stream = [IO.File]::Create($OutputPath)
    try {
        $writer = [IO.BinaryWriter]::new($stream)
        try {
            Write-Header -Writer $writer -Magic 0x31474448 -Rows $rows -Cols $cols -Lon0 $lonValues[0] -Lat0 $latValues[0] -LonStep ($lonValues[1] - $lonValues[0]) -LatStep ($latValues[0] - $latValues[1])
            Write-FloatArray -Writer $writer -Values $latOffsets
            Write-FloatArray -Writer $writer -Values $lonOffsets
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Convert-GeoidGrid {
    param([string]$InputPath, [string]$OutputPath)

    $records = Read-GridRecords -Path $InputPath -ValueCount 1
    $latValues = $records | ForEach-Object Lat | Sort-Object -Descending -Unique
    $lonValues = $records | ForEach-Object Lon | Sort-Object -Unique
    $rows = $latValues.Count
    $cols = $lonValues.Count
    if ($rows * $cols -ne $records.Count) {
        throw "Geoid grid is not rectangular: rows=$rows cols=$cols count=$($records.Count)"
    }

    $values = New-Object double[] ($rows * $cols)
    $byPoint = @{}
    foreach ($record in $records) {
        $byPoint["$($record.Lat)|$($record.Lon)"] = $record.Values[0]
    }

    for ($j = 0; $j -lt $rows; $j++) {
        for ($i = 0; $i -lt $cols; $i++) {
            $key = "$($latValues[$j])|$($lonValues[$i])"
            if (-not $byPoint.ContainsKey($key)) {
                throw "Missing geoid grid point: $key"
            }

            $values[$j * $cols + $i] = $byPoint[$key]
        }
    }

    $stream = [IO.File]::Create($OutputPath)
    try {
        $writer = [IO.BinaryWriter]::new($stream)
        try {
            Write-Header -Writer $writer -Magic 0x31474447 -Rows $rows -Cols $cols -Lon0 $lonValues[0] -Lat0 $latValues[0] -LonStep ($lonValues[1] - $lonValues[0]) -LatStep ($latValues[0] - $latValues[1])
            Write-FloatArray -Writer $writer -Values $values
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$resourcePath = Resolve-Path $ResourceDirectory
Convert-Hd72Grid -InputPath (Join-Path $resourcePath "hu_bme_hd72corr.csv") -OutputPath (Join-Path $resourcePath "hu_bme_hd72corr.hgbin")
Convert-GeoidGrid -InputPath (Join-Path $resourcePath "hu_bme_geoid2014.csv") -OutputPath (Join-Path $resourcePath "hu_bme_geoid2014.hgbin")

Get-ChildItem -Path $resourcePath -Filter *.hgbin | Select-Object Name, Length
