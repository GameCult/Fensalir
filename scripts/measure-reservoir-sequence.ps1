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
        [string]$Label
    )

    $a = [System.Drawing.Bitmap]::new($APath)
    $b = [System.Drawing.Bitmap]::new($BPath)
    try {
        if ($a.Width -ne $b.Width -or $a.Height -ne $b.Height) {
            throw "Capture dimensions differ for $Label."
        }

        $changed = 0
        $sum = 0L
        $max = 0
        for ($y = 0; $y -lt $a.Height; $y++) {
            for ($x = 0; $x -lt $a.Width; $x++) {
                $ca = $a.GetPixel($x, $y)
                $cb = $b.GetPixel($x, $y)
                $delta = [Math]::Max(
                    [Math]::Abs([int]$ca.R - [int]$cb.R),
                    [Math]::Max(
                        [Math]::Abs([int]$ca.G - [int]$cb.G),
                        [Math]::Abs([int]$ca.B - [int]$cb.B)))
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
        [pscustomobject]@{
            Label = $Label
            ChangedPixels = $changed
            TotalPixels = $total
            ChangedPercent = $changed / $total
            MeanMaxChannelDelta = $sum / $total
            MaxChannelDelta = $max
            APath = $APath
            BPath = $BPath
        }
    }
    finally {
        $a.Dispose()
        $b.Dispose()
    }
}

$captures = Import-Csv -Path $ManifestPath
$results = [System.Collections.Generic.List[object]]::new()

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
        }
    }

    foreach ($modeFrames in @($nativeFrames, $baselineFrames)) {
        for ($index = 1; $index -lt $modeFrames.Count; $index++) {
            $previous = $modeFrames[$index - 1]
            $current = $modeFrames[$index]
            $results.Add((Measure-ImageDelta -APath $previous.Path -BPath $current.Path -Label "$suffix temporal-$($current.Mode) f$($previous.ReadyFrames)-f$($current.ReadyFrames)"))
        }
    }
}

$results | Export-Csv -NoTypeInformation -Path $csvPath

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Reservoir Sequence Metrics")
$lines.Add("")
$lines.Add(("Stamp: ``{0}``" -f $Stamp))
$lines.Add("")
$lines.Add("| Metric | Changed pixels | Changed % | Mean max-channel delta | Max channel delta |")
$lines.Add("| --- | ---: | ---: | ---: | ---: |")
foreach ($result in $results) {
    $lines.Add(("| {0} | {1} / {2} | {3} | {4} | {5} |" -f
        $result.Label,
        $result.ChangedPixels,
        $result.TotalPixels,
        $result.ChangedPercent.ToString("P4"),
        $result.MeanMaxChannelDelta.ToString("N4"),
        $result.MaxChannelDelta))
}
$lines.Add("")
$lines.Add("These are sequence probes, not a final ghosting score. Mode deltas show native/baseline disagreement at each sampled time. Temporal deltas show frame-to-frame output movement per mode; the next scorer must use disocclusion/rejection masks or a high-budget reference before claiming quality.")
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8

Write-Host "Reservoir sequence metrics:"
foreach ($result in $results) {
    Write-Host ("  {0}: changed {1}/{2} ({3}) meanMax {4} max {5}" -f
        $result.Label,
        $result.ChangedPixels,
        $result.TotalPixels,
        $result.ChangedPercent.ToString("P4"),
        $result.MeanMaxChannelDelta.ToString("N4"),
        $result.MaxChannelDelta)
}
Write-Host "Wrote:"
Write-Host "  $OutputPath"
Write-Host "  $csvPath"
