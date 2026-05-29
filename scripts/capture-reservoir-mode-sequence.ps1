param(
    [string]$OutputDirectory = "",
    [string]$Stamp = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int[]]$ReadyFrames = @(4, 8, 12, 16),
    [ValidateSet("native", "baseline", "both")]
    [string]$FieldReservoirMode = "both",
    [double]$FieldReservoirScale = 0.5,
    [double]$ReferenceFieldReservoirScale = 0.0,
    [int[]]$RenderDebugModes = @(0, 13),
    [int]$RetainSlots = 4,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($Stamp)) {
    $Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\fensalir-captures"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$clientProject = Join-Path $repoRoot "src\Aquarium.Fensalir\Aquarium.Fensalir.csproj"
$devRoot = Join-Path $repoRoot "artifacts\dev-reload"
$slotRoot = Join-Path $devRoot "slots"
$slotPath = Join-Path $slotRoot ("fensalir-sequence-" + $Stamp + "-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
$cachePath = Join-Path $devRoot "fensalir-sequence-cultcache\aquarium-client.msgpack"
$stdoutLog = Join-Path $devRoot "fensalir-sequence.out.log"
$stderrLog = Join-Path $devRoot "fensalir-sequence.err.log"

New-Item -ItemType Directory -Force -Path $slotPath | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $cachePath) | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Write-Host "Building Fensalir sequence slot:"
Write-Host "  $slotPath"
dotnet build (Join-Path $repoRoot "src\Aquarium.Engine\Aquarium.Engine.csproj") -c Debug -o $slotPath /p:UseAppHost=true
if ($LASTEXITCODE -ne 0) {
    throw "Engine build failed with exit code $LASTEXITCODE."
}

dotnet build $clientProject -c Debug -o $slotPath /p:UseAppHost=false
if ($LASTEXITCODE -ne 0) {
    throw "Fensalir client build failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $slotPath "Aquarium.Engine.exe"
$clientAssembly = Join-Path $slotPath "Aquarium.Fensalir.dll"
$modes = @(if ($FieldReservoirMode -eq "both") { @("native", "baseline") } else { @($FieldReservoirMode) })
if ($ReferenceFieldReservoirScale -gt 0.0) {
    $modes += @("reference")
}
$captures = [System.Collections.Generic.List[object]]::new()

function DebugSuffix {
    param([int]$Mode)

    if ($Mode -eq 0) {
        return "final"
    }
    if ($Mode -eq 13) {
        return "rejection"
    }
    if ($Mode -eq 16) {
        return "domain"
    }
    if ($Mode -eq 17) {
        return "support"
    }
    if ($Mode -eq 18) {
        return "shift"
    }
    if ($Mode -eq 19) {
        return "disocclusion"
    }

    return "debug$Mode"
}

foreach ($mode in $modes) {
    foreach ($readyFrame in $ReadyFrames) {
        foreach ($debugMode in $RenderDebugModes) {
            $captureScale = if ($mode -eq "reference") { $ReferenceFieldReservoirScale } else { $FieldReservoirScale }
            $suffix = DebugSuffix -Mode $debugMode
            $outputPath = Join-Path $OutputDirectory ("reservoir-sequence-$Stamp-$mode-f{0:D4}-$suffix.png" -f $readyFrame)
            $arguments = @(
                "--headless",
                "--headless-width", $Width,
                "--headless-height", $Height,
                "--client-assembly", $clientAssembly,
                "--cache", $cachePath,
                "--shader-source", (Join-Path $slotPath "Render\Shaders\D3D12HeightField.hlsl"),
                "--capture-frame", $outputPath,
                "--field-reservoir-scale", $captureScale
            )
            if ($mode -eq "baseline") {
                $arguments += @("--field-reservoir-mode", "baseline")
            }
            if ($debugMode -gt 0) {
                $arguments += @("--render-debug", $debugMode)
            }

            if (Test-Path -LiteralPath $outputPath) {
                Write-Host ("Skipping existing {0} ready={1} debug={2} -> {3}" -f $mode, $readyFrame, $debugMode, $outputPath)
                $captures.Add([pscustomobject]@{
                    Mode = $mode
                    ReadyFrames = $readyFrame
                    RenderDebugMode = $debugMode
                    ReservoirScale = $captureScale
                    Suffix = $suffix
                    Path = $outputPath
                })
                continue
            }

            Write-Host ("Capturing {0} ready={1} debug={2} -> {3}" -f $mode, $readyFrame, $debugMode, $outputPath)
            $env:AQUARIUM_HEADLESS_READY_FRAMES = [string][Math]::Max(1, $readyFrame)
            $process = Start-Process -FilePath $exePath -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog
            if (-not $process.WaitForExit([Math]::Max(1, $TimeoutSeconds) * 1000)) {
                $process.Kill($true)
                $tail = if (Test-Path $stderrLog) { Get-Content -Path $stderrLog -Tail 80 } else { "<stderr missing>" }
                throw "Fensalir sequence capture timed out after $TimeoutSeconds seconds.`n$($tail -join [Environment]::NewLine)"
            }

            $process.WaitForExit()
            $process.Refresh()
            $exitCode = $process.ExitCode
            if ($null -eq $exitCode -and (Test-Path $outputPath)) {
                $exitCode = 0
            }
            if ($exitCode -ne 0) {
                $tail = if (Test-Path $stderrLog) { Get-Content -Path $stderrLog -Tail 80 } else { "<stderr missing>" }
                throw "Fensalir sequence capture failed with exit code $exitCode.`n$($tail -join [Environment]::NewLine)"
            }
            if (-not (Test-Path $outputPath)) {
                throw "Fensalir sequence capture did not produce expected PNG: $outputPath"
            }

            $captures.Add([pscustomobject]@{
                Mode = $mode
                ReadyFrames = $readyFrame
                RenderDebugMode = $debugMode
                ReservoirScale = $captureScale
                Suffix = $suffix
                Path = $outputPath
            })
        }
    }
}

$manifestPath = Join-Path $OutputDirectory "reservoir-sequence-$Stamp-manifest.csv"
$captures | Export-Csv -NoTypeInformation -Path $manifestPath

if ($RetainSlots -gt 0) {
    Get-ChildItem -Path $slotRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "fensalir-sequence-*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Skip $RetainSlots |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Reservoir sequence captured:"
Write-Host "  Stamp: $Stamp"
Write-Host "  Manifest: $manifestPath"
