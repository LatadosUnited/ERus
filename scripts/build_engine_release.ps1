# Build and Package ERus.Engine

$ErrorActionPreference = "Stop"
$ProjectDir = "$PSScriptRoot\..\ERus.Editor"
$OutDir = "$PSScriptRoot\..\Builds"
$Version = "v0.2.6" # default version, change as needed

if ($args.Count -gt 0) {
    $Version = $args[0]
}

Write-Host "Building ERus Engine ($Version)..."
dotnet build $ProjectDir\ERus.Editor.csproj -c Release

Write-Host "Packaging zip..."
if (!(Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}

$ZipPath = "$OutDir\ERus.Engine-$Version.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path "$ProjectDir\bin\Release\net10.0\*" -DestinationPath $ZipPath -Force

Write-Host "Done! Packaged to $ZipPath"
