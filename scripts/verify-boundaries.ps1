$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$engineRoots = @(
    (Join-Path $repoRoot "src\Aquarium.Engine"),
    (Join-Path $repoRoot "src\Aquarium.Engine.Contracts")
)

$forbiddenEngineTerms = "grid|agent|body|epiphany"
$engineReferences = rg -n -i -- $forbiddenEngineTerms $engineRoots
if ($LASTEXITCODE -eq 0) {
    throw "Fensalir engine and contracts must not contain Epiphany/client policy terms:`n$engineReferences"
}
elseif ($LASTEXITCODE -ne 1) {
    throw "Boundary search for Fensalir engine terms failed."
}

Write-Host "Fensalir boundary checks passed."
