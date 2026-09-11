# deploy_csv_levels_importer.ps1
# Build and deploy CsvLevelsImporter from a local AtasPlatform/Indicators checkout.
#
# Usage:
#   .\deploy_csv_levels_importer.ps1 -IndicatorsRepo "C:\path\to\Indicators"
#
# The script expects CsvLevelsImporter.cs to be copied to:
#   <IndicatorsRepo>\Technical\CsvLevelsImporter.cs

param(
    [Parameter(Mandatory = $true)]
    [string]$IndicatorsRepo,

    [string]$Configuration = "Release",

    [string]$AtasIndicatorsDir = "$env:APPDATA\ATAS\Indicators"
)

$ErrorActionPreference = "Stop"

$repo = Resolve-Path -LiteralPath $IndicatorsRepo
$technicalProject = Join-Path $repo "Technical\Indicators.Technical.csproj"
$sourceFile = Join-Path $repo "Technical\CsvLevelsImporter.cs"

if (-not (Test-Path -LiteralPath $technicalProject)) {
    throw "Technical project not found: $technicalProject"
}

if (-not (Test-Path -LiteralPath $sourceFile)) {
    throw "CsvLevelsImporter.cs not found in Technical folder: $sourceFile"
}

Write-Host "Building ATAS technical indicators..."
dotnet build $technicalProject -c $Configuration --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

$dll = Get-ChildItem -LiteralPath (Join-Path $repo "Technical\bin\$Configuration") `
    -Recurse `
    -Filter "ATAS.Indicators.Technical.dll" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $dll) {
    throw "ATAS.Indicators.Technical.dll not found under Technical\bin\$Configuration."
}

if (-not (Test-Path -LiteralPath $AtasIndicatorsDir)) {
    New-Item -ItemType Directory -Path $AtasIndicatorsDir -Force | Out-Null
}

$destination = Join-Path $AtasIndicatorsDir $dll.Name
Copy-Item -LiteralPath $dll.FullName -Destination $destination -Force

Write-Host "Deployed:"
Write-Host "  $($dll.FullName)"
Write-Host "  -> $destination"
Write-Host ""
Write-Host "Restart ATAS to reload the indicator assembly."
