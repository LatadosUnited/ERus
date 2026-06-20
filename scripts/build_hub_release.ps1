# Build and Package ERus.Hub

$ErrorActionPreference = "Stop"
$ProjectDir = "$PSScriptRoot\..\ERus.Hub"
$OutDir = "$PSScriptRoot\..\Builds"
$Version = "v1.0.0" # default version

if ($args.Count -gt 0) {
    $Version = $args[0]
}

Write-Host "Building ERus Hub ($Version)..."
# Publish as a single file Windows executable for easy distribution
dotnet publish $ProjectDir\ERus.Hub.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$ProjectDir\bin\Release\PublishOutput"

Write-Host "Packaging zip..."
if (!(Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}

$ZipPath = "$OutDir\ERus.Hub-$Version.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path "$ProjectDir\bin\Release\PublishOutput\*" -DestinationPath $ZipPath -Force

Write-Host "Done! ERus.Hub packaged to $ZipPath"
