param(
    [string]$CaptureDirectory = "",
    [string]$Stamp,
    [string]$Prefix = "reservoir-compare",
    [string[]]$Suffixes = @("final", "rejection", "shift"),
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) {
    $CaptureDirectory = Join-Path $repoRoot "artifacts\fensalir-captures"
}

if ([string]::IsNullOrWhiteSpace($Stamp)) {
    throw "Stamp is required, for example: -Stamp 20260529-231904"
}

$CaptureDirectory = [System.IO.Path]::GetFullPath($CaptureDirectory)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $CaptureDirectory "$Prefix-$Stamp-metrics.md"
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$csvPath = [System.IO.Path]::ChangeExtension($OutputPath, ".csv")

Add-Type -AssemblyName System.Drawing

function Measure-ImageDelta {
    param(
        [string]$NativePath,
        [string]$BaselinePath,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $NativePath)) {
        throw "Missing native capture: $NativePath"
    }
    if (-not (Test-Path -LiteralPath $BaselinePath)) {
        throw "Missing baseline capture: $BaselinePath"
    }

    $native = [System.Drawing.Bitmap]::new($NativePath)
    $baseline = [System.Drawing.Bitmap]::new($BaselinePath)
    try {
        if ($native.Width -ne $baseline.Width -or $native.Height -ne $baseline.Height) {
            throw "Capture dimensions differ for $Label."
        }

        $changed = 0
        $sumR = 0L
        $sumG = 0L
        $sumB = 0L
        $maxR = 0
        $maxG = 0
        $maxB = 0
        for ($y = 0; $y -lt $native.Height; $y++) {
            for ($x = 0; $x -lt $native.Width; $x++) {
                $a = $native.GetPixel($x, $y)
                $b = $baseline.GetPixel($x, $y)
                $dr = [Math]::Abs([int]$a.R - [int]$b.R)
                $dg = [Math]::Abs([int]$a.G - [int]$b.G)
                $db = [Math]::Abs([int]$a.B - [int]$b.B)
                if ($dr -ne 0 -or $dg -ne 0 -or $db -ne 0) {
                    $changed++
                }

                $sumR += $dr
                $sumG += $dg
                $sumB += $db
                if ($dr -gt $maxR) { $maxR = $dr }
                if ($dg -gt $maxG) { $maxG = $dg }
                if ($db -gt $maxB) { $maxB = $db }
            }
        }

        $total = $native.Width * $native.Height
        [pscustomobject]@{
            Label = $Label
            Width = $native.Width
            Height = $native.Height
            ChangedPixels = $changed
            TotalPixels = $total
            ChangedPercent = $changed / $total
            MeanR = $sumR / $total
            MeanG = $sumG / $total
            MeanB = $sumB / $total
            MaxR = $maxR
            MaxG = $maxG
            MaxB = $maxB
            NativePath = $NativePath
            BaselinePath = $BaselinePath
        }
    }
    finally {
        $native.Dispose()
        $baseline.Dispose()
    }
}

$results = foreach ($suffix in $Suffixes) {
    $nativePath = Join-Path $CaptureDirectory "$Prefix-$Stamp-native-$suffix.png"
    $baselinePath = Join-Path $CaptureDirectory "$Prefix-$Stamp-baseline-$suffix.png"
    Measure-ImageDelta -NativePath $nativePath -BaselinePath $baselinePath -Label $suffix
}

New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null
$results | Export-Csv -NoTypeInformation -Path $csvPath

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Reservoir Mode Comparison")
$lines.Add("")
$lines.Add(("Stamp: ``{0}``" -f $Stamp))
$lines.Add("")
$lines.Add("| Layer | Changed pixels | Changed % | Mean RGB delta | Max RGB delta |")
$lines.Add("| --- | ---: | ---: | ---: | ---: |")
foreach ($result in $results) {
    $lines.Add(("| {0} | {1} / {2} | {3} | {4}, {5}, {6} | {7}, {8}, {9} |" -f `
        $result.Label,
        $result.ChangedPixels,
        $result.TotalPixels,
        $result.ChangedPercent.ToString("P4"),
        $result.MeanR.ToString("N4"),
        $result.MeanG.ToString("N4"),
        $result.MeanB.ToString("N4"),
        $result.MaxR,
        $result.MaxG,
        $result.MaxB))
}
$lines.Add("")
$lines.Add("This measures native-domain mode against texel-baseline mode for already captured frames. It proves the comparison switch changes visible output; it is not a temporal leakage or ghosting score by itself.")
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8

Write-Host "Reservoir comparison metrics:"
foreach ($result in $results) {
    Write-Host ("  {0}: changed {1}/{2} ({3}) mean RGB ({4}, {5}, {6}) max RGB ({7}, {8}, {9})" -f
        $result.Label,
        $result.ChangedPixels,
        $result.TotalPixels,
        $result.ChangedPercent.ToString("P4"),
        $result.MeanR.ToString("N4"),
        $result.MeanG.ToString("N4"),
        $result.MeanB.ToString("N4"),
        $result.MaxR,
        $result.MaxG,
        $result.MaxB)
}
Write-Host "Wrote:"
Write-Host "  $OutputPath"
Write-Host "  $csvPath"
