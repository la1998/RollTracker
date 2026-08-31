param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "RollTracker.sln"
$devOutput = Join-Path $repoRoot "RollTracker\bin\x64\$Configuration"
$devPluginDll = Join-Path $devOutput "RollTracker.dll"

Write-Host "Building RollTracker local dev plugin ($Configuration)..." -ForegroundColor Cyan
dotnet build $solution -c $Configuration -p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "RollTracker local dev build failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "Done. Dalamud dev plugin DLL:" -ForegroundColor Green
Write-Host $devPluginDll
Write-Host ""
Write-Host "Keep the repo-installed RollTracker disabled while this dev build is active."
