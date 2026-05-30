param(
    [string]$CaptureDirectory = "",
    [string]$Stamp,
    [string]$ManifestPath = "",
    [string]$CandidateMode = "native",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) {
    $CaptureDirectory = Join-Path $repoRoot "artifacts\fensalir-captures"
}
if ([string]::IsNullOrWhiteSpace($Stamp)) {
    throw "Stamp is required, for example: -Stamp 20260530-003006"
}

$CaptureDirectory = [System.IO.Path]::GetFullPath($CaptureDirectory)
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $CaptureDirectory "reservoir-sequence-$Stamp-manifest.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $CaptureDirectory "reservoir-loss-curve-$Stamp.md"
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
        [int]$ReadyFrames,
        [string]$Suffix
    )

    $key = "{0}|{1}|{2}" -f $Mode, $ReadyFrames, $Suffix
    if ($byKey.ContainsKey($key)) {
        return $byKey[$key]
    }

    return $null
}

function Find-PreferredMaskPath {
    param(
        [string]$Mode,
        [int]$ReadyFrames
    )

    $disocclusion = Find-Capture -Mode $Mode -ReadyFrames $ReadyFrames -Suffix "disocclusion"
    if ($null -ne $disocclusion) {
        return $disocclusion.Path
    }

    $rejection = Find-Capture -Mode $Mode -ReadyFrames $ReadyFrames -Suffix "rejection"
    if ($null -ne $rejection) {
        return $rejection.Path
    }

    return ""
}

function Measure-Loss {
    param(
        [string]$CandidatePath,
        [string]$ReferencePath,
        [string]$Label,
        [int]$ReadyFrames,
        [string]$CandidateMaskPath = "",
        [string]$ReferenceMaskPath = ""
    )

    $candidate = [System.Drawing.Bitmap]::new($CandidatePath)
    $reference = [System.Drawing.Bitmap]::new($ReferencePath)
    $candidateMask = if ([string]::IsNullOrWhiteSpace($CandidateMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($CandidateMaskPath) }
    $referenceMask = if ([string]::IsNullOrWhiteSpace($ReferenceMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($ReferenceMaskPath) }
    try {
        if ($candidate.Width -ne $reference.Width -or $candidate.Height -ne $reference.Height) {
            throw "Capture dimensions differ for $Label."
        }
        if ($candidateMask -ne $null -and ($candidateMask.Width -ne $candidate.Width -or $candidateMask.Height -ne $candidate.Height)) {
            throw "Candidate mask dimensions differ for $Label."
        }
        if ($referenceMask -ne $null -and ($referenceMask.Width -ne $candidate.Width -or $referenceMask.Height -ne $candidate.Height)) {
            throw "Reference mask dimensions differ for $Label."
        }

        $measuredPixels = 0
        $changedPixels = 0
        $absoluteErrorSum = 0.0
        $squaredErrorSum = 0.0
        $maxChannelDelta = 0
        for ($y = 0; $y -lt $candidate.Height; $y++) {
            for ($x = 0; $x -lt $candidate.Width; $x++) {
                $maskActive = $true
                if ($candidateMask -ne $null -or $referenceMask -ne $null) {
                    $maskActive = $false
                    if ($candidateMask -ne $null) {
                        $maskColor = $candidateMask.GetPixel($x, $y)
                        $maskActive = $maskActive -or ([Math]::Max([int]$maskColor.R, [Math]::Max([int]$maskColor.G, [int]$maskColor.B)) -gt 12)
                    }
                    if ($referenceMask -ne $null) {
                        $maskColor = $referenceMask.GetPixel($x, $y)
                        $maskActive = $maskActive -or ([Math]::Max([int]$maskColor.R, [Math]::Max([int]$maskColor.G, [int]$maskColor.B)) -gt 12)
                    }
                }

                if (-not $maskActive) {
                    continue
                }

                $a = $candidate.GetPixel($x, $y)
                $b = $reference.GetPixel($x, $y)
                $dr = [int]$a.R - [int]$b.R
                $dg = [int]$a.G - [int]$b.G
                $db = [int]$a.B - [int]$b.B
                $maxDelta = [Math]::Max([Math]::Abs($dr), [Math]::Max([Math]::Abs($dg), [Math]::Abs($db)))
                if ($maxDelta -gt 0) {
                    $changedPixels++
                }

                $absoluteErrorSum += [Math]::Abs($dr) + [Math]::Abs($dg) + [Math]::Abs($db)
                $squaredErrorSum += ($dr * $dr) + ($dg * $dg) + ($db * $db)
                if ($maxDelta -gt $maxChannelDelta) {
                    $maxChannelDelta = $maxDelta
                }
                $measuredPixels++
            }
        }

        $totalPixels = $candidate.Width * $candidate.Height
        $pixelDenominator = if ($measuredPixels -gt 0) { $measuredPixels } else { 1 }
        $channelDenominator = $pixelDenominator * 3.0
        $mse = $squaredErrorSum / $channelDenominator
        $rmse = [Math]::Sqrt($mse)
        $psnr = if ($mse -le 0.0) { [double]::PositiveInfinity } else { 10.0 * [Math]::Log10((255.0 * 255.0) / $mse) }

        [pscustomobject]@{
            Label = $Label
            ReadyFrames = $ReadyFrames
            MeasuredPixels = $measuredPixels
            TotalPixels = $totalPixels
            FramePercent = $measuredPixels / $totalPixels
            ChangedPixels = $changedPixels
            ChangedPercent = $changedPixels / $pixelDenominator
            MAE = $absoluteErrorSum / $channelDenominator
            MSE = $mse
            RMSE = $rmse
            PSNR = $psnr
            MaxChannelDelta = $maxChannelDelta
            CandidatePath = $CandidatePath
            ReferencePath = $ReferencePath
            CandidateMaskPath = $CandidateMaskPath
            ReferenceMaskPath = $ReferenceMaskPath
        }
    }
    finally {
        if ($candidateMask -ne $null) {
            $candidateMask.Dispose()
        }
        if ($referenceMask -ne $null) {
            $referenceMask.Dispose()
        }
        $candidate.Dispose()
        $reference.Dispose()
    }
}

$readyFrames = @($captures |
    Where-Object { $_.Mode -eq $CandidateMode -and $_.Suffix -eq "final" } |
    ForEach-Object { [int]$_.ReadyFrames } |
    Sort-Object -Unique)
if ($readyFrames.Count -eq 0) {
    throw "No final candidate captures found for mode '$CandidateMode'."
}

$results = [System.Collections.Generic.List[object]]::new()
foreach ($readyFrame in $readyFrames) {
    $candidate = Find-Capture -Mode $CandidateMode -ReadyFrames $readyFrame -Suffix "final"
    $reference = Find-Capture -Mode "reference" -ReadyFrames $readyFrame -Suffix "final"
    if ($null -eq $candidate -or $null -eq $reference) {
        continue
    }

    $results.Add((Measure-Loss -CandidatePath $candidate.Path -ReferencePath $reference.Path -Label "final" -ReadyFrames $readyFrame))

    $candidateMask = Find-PreferredMaskPath -Mode $CandidateMode -ReadyFrames $readyFrame
    $referenceMask = Find-PreferredMaskPath -Mode "reference" -ReadyFrames $readyFrame
    if (-not [string]::IsNullOrWhiteSpace($candidateMask) -or -not [string]::IsNullOrWhiteSpace($referenceMask)) {
        $results.Add((Measure-Loss -CandidatePath $candidate.Path -ReferencePath $reference.Path -Label "masked" -ReadyFrames $readyFrame -CandidateMaskPath $candidateMask -ReferenceMaskPath $referenceMask))
    }
}

if ($results.Count -eq 0) {
    throw "No candidate/reference final pairs found for stamp '$Stamp'."
}

$results | Export-Csv -NoTypeInformation -Path $csvPath

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Reservoir Reconstruction Loss Curve")
$lines.Add("")
$lines.Add(("Stamp: ``{0}``" -f $Stamp))
$lines.Add(("Candidate mode: ``{0}``" -f $CandidateMode))
$lines.Add("")
$lines.Add("| Scope | Ready frames | Measured/frame % | Changed % | MAE | MSE | RMSE | PSNR dB | Max delta |")
$lines.Add("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
foreach ($result in ($results | Sort-Object ReadyFrames, Label)) {
    $psnrText = if ([double]::IsInfinity($result.PSNR)) { "inf" } else { $result.PSNR.ToString("N3") }
    $lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |" -f
        $result.Label,
        $result.ReadyFrames,
        $result.FramePercent.ToString("P4"),
        $result.ChangedPercent.ToString("P4"),
        $result.MAE.ToString("N4"),
        $result.MSE.ToString("N4"),
        $result.RMSE.ToString("N4"),
        $psnrText,
        $result.MaxChannelDelta))
}
$lines.Add("")
$lines.Add("Loss is RGB reconstruction error against the same-time native-domain reference capture. The reference is higher work-grid scale and higher spatial reuse budget when the manifest was captured that way; it is not an offline path-traced ground truth.")
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8

Write-Host "Reservoir reconstruction loss curve:"
foreach ($result in ($results | Sort-Object ReadyFrames, Label)) {
    $psnrText = if ([double]::IsInfinity($result.PSNR)) { "inf" } else { $result.PSNR.ToString("N3") }
    Write-Host ("  {0} f{1}: measured {2} changed {3} MAE {4} RMSE {5} PSNR {6} dB max {7}" -f
        $result.Label,
        $result.ReadyFrames,
        $result.FramePercent.ToString("P4"),
        $result.ChangedPercent.ToString("P4"),
        $result.MAE.ToString("N4"),
        $result.RMSE.ToString("N4"),
        $psnrText,
        $result.MaxChannelDelta)
}
Write-Host "Wrote:"
Write-Host "  $OutputPath"
Write-Host "  $csvPath"
