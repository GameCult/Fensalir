param(
    [string]$OutputPath,
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$ReadyFrames = 8,
    [int]$RenderDebugMode = -1,
    [ValidateSet("native", "baseline")]
    [string]$FieldReservoirMode = "native",
    [int]$RetainSlots = 4,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot ("artifacts\fensalir-captures\fensalir-fixed-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".png")
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$clientProject = Join-Path $repoRoot "src\Aquarium.Fensalir\Aquarium.Fensalir.csproj"
$devRoot = Join-Path $repoRoot "artifacts\dev-reload"
$slotRoot = Join-Path $devRoot "slots"
$slotPath = Join-Path $slotRoot ("fensalir-capture-" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
$cachePath = Join-Path $devRoot "fensalir-capture-cultcache\aquarium-client.msgpack"
$stdoutLog = Join-Path $devRoot "fensalir-capture.out.log"
$stderrLog = Join-Path $devRoot "fensalir-capture.err.log"

New-Item -ItemType Directory -Force -Path $slotPath | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $cachePath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null

Write-Host "Building Fensalir capture slot:"
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
$arguments = @(
    "--headless",
    "--headless-width", $Width,
    "--headless-height", $Height,
    "--client-assembly", $clientAssembly,
    "--cache", $cachePath,
    "--shader-source", (Join-Path $slotPath "Render\Shaders\D3D12HeightField.hlsl"),
    "--capture-frame", $OutputPath
)
if ($RenderDebugMode -ge 0) {
    $arguments += @("--render-debug", $RenderDebugMode)
}
if ($FieldReservoirMode -eq "baseline") {
    $arguments += @("--field-reservoir-mode", "baseline")
}

$env:AQUARIUM_HEADLESS_READY_FRAMES = [string][Math]::Max(1, $ReadyFrames)
$process = Start-Process -FilePath $exePath -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog
if (-not $process.WaitForExit([Math]::Max(1, $TimeoutSeconds) * 1000)) {
    $process.Kill($true)
    $tail = if (Test-Path $stderrLog) { Get-Content -Path $stderrLog -Tail 80 } else { "<stderr missing>" }
    throw "Fensalir capture timed out after $TimeoutSeconds seconds.`n$($tail -join [Environment]::NewLine)"
}

$process.WaitForExit()
$process.Refresh()
$exitCode = $process.ExitCode
if ($null -eq $exitCode -and (Test-Path $OutputPath)) {
    $exitCode = 0
}

if ($exitCode -ne 0) {
    $tail = if (Test-Path $stderrLog) { Get-Content -Path $stderrLog -Tail 80 } else { "<stderr missing>" }
    throw "Fensalir capture failed with exit code $exitCode.`n$($tail -join [Environment]::NewLine)"
}

if (-not (Test-Path $OutputPath)) {
    throw "Fensalir capture did not produce expected PNG: $OutputPath"
}

if ($RetainSlots -gt 0) {
    Get-ChildItem -Path $slotRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "fensalir-capture-*" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -Skip $RetainSlots |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Fensalir frame captured:"
Write-Host "  $OutputPath"
