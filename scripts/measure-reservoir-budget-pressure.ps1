param(
    [string]$CaptureDirectory = "",
    [string]$Stamp,
    [string]$ManifestPath = "",
    [string]$CandidateMode = "native",
    [int]$ReadyFrames = 0,
    [int]$TileSize = 32,
    [double]$HotTilePercent = 0.10,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) {
    $CaptureDirectory = Join-Path $repoRoot "artifacts\fensalir-captures"
}
if ([string]::IsNullOrWhiteSpace($Stamp)) {
    throw "Stamp is required, for example: -Stamp 20260530-001046"
}
if ($TileSize -lt 4) {
    throw "TileSize must be at least 4."
}
if ($HotTilePercent -le 0.0 -or $HotTilePercent -gt 1.0) {
    throw "HotTilePercent must be in the range (0, 1]."
}

$CaptureDirectory = [System.IO.Path]::GetFullPath($CaptureDirectory)
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $CaptureDirectory "reservoir-sequence-$Stamp-manifest.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $CaptureDirectory "reservoir-budget-pressure-$Stamp.md"
}
$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$csvPath = [System.IO.Path]::ChangeExtension($OutputPath, ".csv")

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Missing sequence manifest: $ManifestPath"
}

Add-Type -AssemblyName System.Drawing

$captures = Import-Csv -Path $ManifestPath
$byKey = @{}
foreach ($capture in $captures) {
    $key = "{0}|{1}|{2}" -f $capture.Mode, $capture.ReadyFrames, $capture.Suffix
    $byKey[$key] = $capture
}

function Find-Capture {
    param(
        [string]$Mode,
        [int]$Frame,
        [string]$Suffix
    )

    $key = "{0}|{1}|{2}" -f $Mode, $Frame, $Suffix
    if ($byKey.ContainsKey($key)) {
        return $byKey[$key]
    }

    return $null
}

function Find-PreferredMask {
    param(
        [string]$Mode,
        [int]$Frame
    )

    $disocclusion = Find-Capture -Mode $Mode -Frame $Frame -Suffix "disocclusion"
    if ($null -ne $disocclusion) {
        return $disocclusion.Path
    }

    $rejection = Find-Capture -Mode $Mode -Frame $Frame -Suffix "rejection"
    if ($null -ne $rejection) {
        return $rejection.Path
    }

    return ""
}

if ($ReadyFrames -le 0) {
    $candidateFrames = @($captures |
        Where-Object { $_.Mode -eq $CandidateMode -and $_.Suffix -eq "final" } |
        Sort-Object { [int]$_.ReadyFrames })
    if ($candidateFrames.Count -eq 0) {
        throw "No final captures found for mode '$CandidateMode'."
    }

    $ReadyFrames = [int]$candidateFrames[0].ReadyFrames
}

$candidate = Find-Capture -Mode $CandidateMode -Frame $ReadyFrames -Suffix "final"
$reference = Find-Capture -Mode "reference" -Frame $ReadyFrames -Suffix "final"
if ($null -eq $candidate) {
    throw "Missing final candidate capture for mode '$CandidateMode' frame $ReadyFrames."
}
if ($null -eq $reference) {
    throw "Missing final reference capture for frame $ReadyFrames."
}

$candidateMaskPath = Find-PreferredMask -Mode $CandidateMode -Frame $ReadyFrames
$referenceMaskPath = Find-PreferredMask -Mode "reference" -Frame $ReadyFrames

$candidateImage = [System.Drawing.Bitmap]::new($candidate.Path)
$referenceImage = [System.Drawing.Bitmap]::new($reference.Path)
$candidateMask = if ([string]::IsNullOrWhiteSpace($candidateMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($candidateMaskPath) }
$referenceMask = if ([string]::IsNullOrWhiteSpace($referenceMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($referenceMaskPath) }

try {
    if ($candidateImage.Width -ne $referenceImage.Width -or $candidateImage.Height -ne $referenceImage.Height) {
        throw "Candidate and reference dimensions differ."
    }
    if ($candidateMask -ne $null -and ($candidateMask.Width -ne $candidateImage.Width -or $candidateMask.Height -ne $candidateImage.Height)) {
        throw "Candidate mask dimensions differ."
    }
    if ($referenceMask -ne $null -and ($referenceMask.Width -ne $candidateImage.Width -or $referenceMask.Height -ne $candidateImage.Height)) {
        throw "Reference mask dimensions differ."
    }

    $tilesX = [int][Math]::Ceiling($candidateImage.Width / [double]$TileSize)
    $tilesY = [int][Math]::Ceiling($candidateImage.Height / [double]$TileSize)
    $tileRows = [System.Collections.Generic.List[object]]::new()

    for ($tileY = 0; $tileY -lt $tilesY; $tileY++) {
        for ($tileX = 0; $tileX -lt $tilesX; $tileX++) {
            $x0 = $tileX * $TileSize
            $y0 = $tileY * $TileSize
            $x1 = [Math]::Min($x0 + $TileSize, $candidateImage.Width)
            $y1 = [Math]::Min($y0 + $TileSize, $candidateImage.Height)
            $tilePixels = ($x1 - $x0) * ($y1 - $y0)
            $maskedPixels = 0
            $changedPixels = 0
            $sumDelta = 0L
            $maxDelta = 0

            for ($y = $y0; $y -lt $y1; $y++) {
                for ($x = $x0; $x -lt $x1; $x++) {
                    $maskActive = $true
                    if ($candidateMask -ne $null -or $referenceMask -ne $null) {
                        $maskActive = $false
                        if ($candidateMask -ne $null) {
                            $cm = $candidateMask.GetPixel($x, $y)
                            $maskActive = $maskActive -or ([Math]::Max([int]$cm.R, [Math]::Max([int]$cm.G, [int]$cm.B)) -gt 12)
                        }
                        if ($referenceMask -ne $null) {
                            $rm = $referenceMask.GetPixel($x, $y)
                            $maskActive = $maskActive -or ([Math]::Max([int]$rm.R, [Math]::Max([int]$rm.G, [int]$rm.B)) -gt 12)
                        }
                    }

                    if (-not $maskActive) {
                        continue
                    }

                    $a = $candidateImage.GetPixel($x, $y)
                    $b = $referenceImage.GetPixel($x, $y)
                    $delta = [Math]::Max(
                        [Math]::Abs([int]$a.R - [int]$b.R),
                        [Math]::Max(
                            [Math]::Abs([int]$a.G - [int]$b.G),
                            [Math]::Abs([int]$a.B - [int]$b.B)))

                    $maskedPixels++
                    if ($delta -gt 0) {
                        $changedPixels++
                    }
                    $sumDelta += $delta
                    if ($delta -gt $maxDelta) {
                        $maxDelta = $delta
                    }
                }
            }

            $measuredDenominator = if ($maskedPixels -gt 0) { $maskedPixels } else { 1 }
            $tileRows.Add([pscustomobject]@{
                TileX = $tileX
                TileY = $tileY
                X = $x0
                Y = $y0
                Width = $x1 - $x0
                Height = $y1 - $y0
                TilePixels = $tilePixels
                MeasuredPixels = $maskedPixels
                MaskCoverage = $maskedPixels / $tilePixels
                ChangedPixels = $changedPixels
                ChangedPercent = $changedPixels / $measuredDenominator
                MeanMaxChannelDelta = $sumDelta / $measuredDenominator
                MaxChannelDelta = $maxDelta
                PressureScore = ($sumDelta / $measuredDenominator) * ($maskedPixels / $tilePixels)
            })
        }
    }

    $ranked = @($tileRows | Sort-Object PressureScore -Descending)
    $hotCount = [Math]::Max(1, [int][Math]::Ceiling($ranked.Count * $HotTilePercent))
    $hotTiles = @($ranked | Select-Object -First $hotCount)
    $measuredPixels = ($tileRows | Measure-Object -Property MeasuredPixels -Sum).Sum
    $totalPixels = ($tileRows | Measure-Object -Property TilePixels -Sum).Sum
    $totalDelta = 0.0
    foreach ($row in $tileRows) {
        $totalDelta += $row.MeanMaxChannelDelta * $row.MeasuredPixels
    }
    $hotMeasuredPixels = ($hotTiles | Measure-Object -Property MeasuredPixels -Sum).Sum
    $hotDelta = 0.0
    foreach ($row in $hotTiles) {
        $hotDelta += $row.MeanMaxChannelDelta * $row.MeasuredPixels
    }
    $capturedDeltaShare = if ($totalDelta -gt 0.0) { $hotDelta / $totalDelta } else { 0.0 }

    $rank = 1
    $exportRows = foreach ($row in $ranked) {
        [pscustomobject]@{
            Rank = $rank++
            TileX = $row.TileX
            TileY = $row.TileY
            X = $row.X
            Y = $row.Y
            Width = $row.Width
            Height = $row.Height
            TilePixels = $row.TilePixels
            MeasuredPixels = $row.MeasuredPixels
            MaskCoverage = $row.MaskCoverage
            ChangedPixels = $row.ChangedPixels
            ChangedPercent = $row.ChangedPercent
            MeanMaxChannelDelta = $row.MeanMaxChannelDelta
            MaxChannelDelta = $row.MaxChannelDelta
            PressureScore = $row.PressureScore
        }
    }
    $exportRows | Export-Csv -NoTypeInformation -Path $csvPath

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Reservoir Budget Pressure")
    $lines.Add("")
    $lines.Add(("Stamp: ``{0}``" -f $Stamp))
    $lines.Add(("Candidate: ``{0}``, ready frame ``{1}``" -f $CandidateMode, $ReadyFrames))
    $lines.Add(("Reference: higher-work-grid native-domain ``reference``"))
    $lines.Add(("Tile size: ``{0}``; hot tile share: ``{1}``" -f $TileSize, $HotTilePercent.ToString("P2")))
    $lines.Add(("Mask sources: candidate ``{0}``, reference ``{1}``" -f $candidateMaskPath, $referenceMaskPath))
    $lines.Add("")
    $lines.Add(("Measured pixels: ``{0}`` / ``{1}`` ({2})" -f $measuredPixels, $totalPixels, ($measuredPixels / $totalPixels).ToString("P4")))
    $lines.Add(("Top {0} of {1} tiles carry {2} of measured reference-error delta." -f $hotCount, $ranked.Count, $capturedDeltaShare.ToString("P4")))
    $lines.Add("")
    $lines.Add("| Rank | Tile | Origin | Mask/frame % | Changed % | Mean max-channel delta | Max delta | Pressure |")
    $lines.Add("| ---: | --- | --- | ---: | ---: | ---: | ---: | ---: |")
    foreach ($row in ($ranked | Select-Object -First ([Math]::Min(12, $ranked.Count)))) {
        $lines.Add(("| {0} | {1},{2} | {3},{4} | {5} | {6} | {7} | {8} | {9} |" -f
            ($ranked.IndexOf($row) + 1),
            $row.TileX,
            $row.TileY,
            $row.X,
            $row.Y,
            $row.MaskCoverage.ToString("P4"),
            $row.ChangedPercent.ToString("P4"),
            $row.MeanMaxChannelDelta.ToString("N4"),
            $row.MaxChannelDelta,
            $row.PressureScore.ToString("N4")))
    }
    $lines.Add("")
    $lines.Add("PressureScore is mean max-channel reference error multiplied by mask coverage. It is an offline allocator probe: it identifies where extra reservoir update work may buy visible stability, but it does not own visibility or runtime scheduling.")
    Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8

    Write-Host "Reservoir budget pressure:"
    Write-Host ("  measured {0}/{1} pixels ({2})" -f $measuredPixels, $totalPixels, ($measuredPixels / $totalPixels).ToString("P4"))
    Write-Host ("  top {0}/{1} tiles carry {2} of measured reference-error delta" -f $hotCount, $ranked.Count, $capturedDeltaShare.ToString("P4"))
    Write-Host "Wrote:"
    Write-Host "  $OutputPath"
    Write-Host "  $csvPath"
}
finally {
    if ($candidateMask -ne $null) {
        $candidateMask.Dispose()
    }
    if ($referenceMask -ne $null) {
        $referenceMask.Dispose()
    }
    $candidateImage.Dispose()
    $referenceImage.Dispose()
}
