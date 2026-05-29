param(
    [string]$CaptureDirectory = "",
    [string]$Stamp,
    [string]$ManifestPath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) {
    $CaptureDirectory = Join-Path $repoRoot "artifacts\fensalir-captures"
}
if ([string]::IsNullOrWhiteSpace($Stamp)) {
    throw "Stamp is required, for example: -Stamp 20260529-234500"
}

$CaptureDirectory = [System.IO.Path]::GetFullPath($CaptureDirectory)
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $CaptureDirectory "reservoir-sequence-$Stamp-manifest.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $CaptureDirectory "reservoir-sequence-$Stamp-metrics.md"
}
$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$csvPath = [System.IO.Path]::ChangeExtension($OutputPath, ".csv")

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Missing sequence manifest: $ManifestPath"
}

Add-Type -AssemblyName System.Drawing

function Measure-ImageDelta {
    param(
        [string]$APath,
        [string]$BPath,
        [string]$Label,
        [string]$AMaskPath = "",
        [string]$BMaskPath = ""
    )

    $a = [System.Drawing.Bitmap]::new($APath)
    $b = [System.Drawing.Bitmap]::new($BPath)
    $aMask = if ([string]::IsNullOrWhiteSpace($AMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($AMaskPath) }
    $bMask = if ([string]::IsNullOrWhiteSpace($BMaskPath)) { $null } else { [System.Drawing.Bitmap]::new($BMaskPath) }
    try {
        if ($a.Width -ne $b.Width -or $a.Height -ne $b.Height) {
            throw "Capture dimensions differ for $Label."
        }
        if ($aMask -ne $null -and ($aMask.Width -ne $a.Width -or $aMask.Height -ne $a.Height)) {
            throw "A-mask dimensions differ for $Label."
        }
        if ($bMask -ne $null -and ($bMask.Width -ne $a.Width -or $bMask.Height -ne $a.Height)) {
            throw "B-mask dimensions differ for $Label."
        }

        $changed = 0
        $measured = 0
        $sum = 0L
        $max = 0
        for ($y = 0; $y -lt $a.Height; $y++) {
            for ($x = 0; $x -lt $a.Width; $x++) {
                $maskActive = $true
                if ($aMask -ne $null -or $bMask -ne $null) {
                    $maskActive = $false
                    if ($aMask -ne $null) {
                        $ma = $aMask.GetPixel($x, $y)
                        $maskActive = $maskActive -or ([Math]::Max([int]$ma.R, [Math]::Max([int]$ma.G, [int]$ma.B)) -gt 12)
                    }
                    if ($bMask -ne $null) {
                        $mb = $bMask.GetPixel($x, $y)
                        $maskActive = $maskActive -or ([Math]::Max([int]$mb.R, [Math]::Max([int]$mb.G, [int]$mb.B)) -gt 12)
                    }
                }

                if (-not $maskActive) {
                    continue
                }

                $ca = $a.GetPixel($x, $y)
                $cb = $b.GetPixel($x, $y)
                $delta = [Math]::Max(
                    [Math]::Abs([int]$ca.R - [int]$cb.R),
                    [Math]::Max(
                        [Math]::Abs([int]$ca.G - [int]$cb.G),
                        [Math]::Abs([int]$ca.B - [int]$cb.B)))
                $measured++
                if ($delta -gt 0) {
                    $changed++
                }

                $sum += $delta
                if ($delta -gt $max) {
                    $max = $delta
                }
            }
        }

        $total = $a.Width * $a.Height
        $denominator = if ($measured -gt 0) { $measured } else { 1 }
        [pscustomobject]@{
            Label = $Label
            ChangedPixels = $changed
            MeasuredPixels = $measured
            TotalPixels = $total
            ChangedPercent = $changed / $denominator
            FramePercent = $measured / $total
            MeanMaxChannelDelta = $sum / $denominator
            MaxChannelDelta = $max
            APath = $APath
            BPath = $BPath
            AMaskPath = $AMaskPath
            BMaskPath = $BMaskPath
        }
    }
    finally {
        if ($aMask -ne $null) {
            $aMask.Dispose()
        }
        if ($bMask -ne $null) {
            $bMask.Dispose()
        }
        $a.Dispose()
        $b.Dispose()
    }
}

$captures = Import-Csv -Path $ManifestPath
$results = [System.Collections.Generic.List[object]]::new()
$byKey = @{}
foreach ($capture in $captures) {
    $key = "{0}|{1}|{2}" -f $capture.Mode, $capture.ReadyFrames, $capture.Suffix
    $byKey[$key] = $capture
}

$groups = $captures | Group-Object Suffix
foreach ($group in $groups) {
    $suffix = $group.Name
    $nativeFrames = $group.Group |
        Where-Object { $_.Mode -eq "native" } |
        Sort-Object { [int]$_.ReadyFrames }
    $baselineFrames = $group.Group |
        Where-Object { $_.Mode -eq "baseline" } |
        Sort-Object { [int]$_.ReadyFrames }

    foreach ($native in $nativeFrames) {
        $baseline = $baselineFrames | Where-Object { [int]$_.ReadyFrames -eq [int]$native.ReadyFrames } | Select-Object -First 1
        if ($null -ne $baseline) {
            $results.Add((Measure-ImageDelta -APath $native.Path -BPath $baseline.Path -Label "$suffix mode-delta f$($native.ReadyFrames)"))
            if ($suffix -eq "final") {
                $nativeMaskKey = "native|{0}|rejection" -f $native.ReadyFrames
                $baselineMaskKey = "baseline|{0}|rejection" -f $native.ReadyFrames
                if ($byKey.ContainsKey($nativeMaskKey) -or $byKey.ContainsKey($baselineMaskKey)) {
                    $nativeMask = if ($byKey.ContainsKey($nativeMaskKey)) { $byKey[$nativeMaskKey].Path } else { "" }
                    $baselineMask = if ($byKey.ContainsKey($baselineMaskKey)) { $byKey[$baselineMaskKey].Path } else { "" }
                    $results.Add((Measure-ImageDelta -APath $native.Path -BPath $baseline.Path -Label "final masked-mode-delta f$($native.ReadyFrames)" -AMaskPath $nativeMask -BMaskPath $baselineMask))
                }
            }
        }
    }

    foreach ($modeFrames in @($nativeFrames, $baselineFrames)) {
        for ($index = 1; $index -lt $modeFrames.Count; $index++) {
            $previous = $modeFrames[$index - 1]
            $current = $modeFrames[$index]
            $results.Add((Measure-ImageDelta -APath $previous.Path -BPath $current.Path -Label "$suffix temporal-$($current.Mode) f$($previous.ReadyFrames)-f$($current.ReadyFrames)"))
            if ($suffix -eq "final") {
                $previousMaskKey = "{0}|{1}|rejection" -f $current.Mode, $previous.ReadyFrames
                $currentMaskKey = "{0}|{1}|rejection" -f $current.Mode, $current.ReadyFrames
                if ($byKey.ContainsKey($previousMaskKey) -or $byKey.ContainsKey($currentMaskKey)) {
                    $previousMask = if ($byKey.ContainsKey($previousMaskKey)) { $byKey[$previousMaskKey].Path } else { "" }
                    $currentMask = if ($byKey.ContainsKey($currentMaskKey)) { $byKey[$currentMaskKey].Path } else { "" }
                    $results.Add((Measure-ImageDelta -APath $previous.Path -BPath $current.Path -Label "final masked-temporal-$($current.Mode) f$($previous.ReadyFrames)-f$($current.ReadyFrames)" -AMaskPath $previousMask -BMaskPath $currentMask))
                }
            }
        }
    }
}

$results | Export-Csv -NoTypeInformation -Path $csvPath

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Reservoir Sequence Metrics")
$lines.Add("")
$lines.Add(("Stamp: ``{0}``" -f $Stamp))
$lines.Add("")
$lines.Add("| Metric | Changed pixels | Mask/frame % | Changed % | Mean max-channel delta | Max channel delta |")
$lines.Add("| --- | ---: | ---: | ---: | ---: | ---: |")
foreach ($result in $results) {
    $lines.Add(("| {0} | {1} / {2} | {3} | {4} | {5} | {6} |" -f
        $result.Label,
        $result.ChangedPixels,
        $result.MeasuredPixels,
        $result.FramePercent.ToString("P4"),
        $result.ChangedPercent.ToString("P4"),
        $result.MeanMaxChannelDelta.ToString("N4"),
        $result.MaxChannelDelta))
}
$lines.Add("")
$lines.Add("These are sequence probes, not a final ghosting score. Mode deltas show native/baseline disagreement at each sampled time. Temporal deltas show frame-to-frame output movement per mode. When rejection captures are present, masked rows restrict the comparison to pixels marked by either mode's rejection debug output; a high-budget reference is still needed before claiming quality.")
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8

Write-Host "Reservoir sequence metrics:"
foreach ($result in $results) {
    Write-Host ("  {0}: changed {1}/{2} mask/frame {3} changed {4} meanMax {5} max {6}" -f
        $result.Label,
        $result.ChangedPixels,
        $result.MeasuredPixels,
        $result.FramePercent.ToString("P4"),
        $result.ChangedPercent.ToString("P4"),
        $result.MeanMaxChannelDelta.ToString("N4"),
        $result.MaxChannelDelta)
}
Write-Host "Wrote:"
Write-Host "  $OutputPath"
Write-Host "  $csvPath"
