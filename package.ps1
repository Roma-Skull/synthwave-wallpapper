#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = '1.0.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$ExePath = Join-Path $Root 'NeonHorizonWallpaper.exe'

& (Join-Path $Root 'build.ps1') -OutExe $ExePath

$DistDir = Join-Path $Root 'dist'
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$CleanVer = $Version.TrimStart('v', 'V')
$PackageName = "NeonHorizonWallpaper-v$CleanVer-win"
$StageDir = Join-Path $DistDir $PackageName
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

Copy-Item -Path $ExePath -Destination (Join-Path $StageDir 'NeonHorizonWallpaper.exe') -Force
Copy-Item -Path (Join-Path $Root 'README.md') -Destination (Join-Path $StageDir 'README.md') -Force

$ZipPath = Join-Path $DistDir "$PackageName.zip"
Compress-Archive -Path "$StageDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal -Force

$DistExePath = Join-Path $DistDir 'NeonHorizonWallpaper.exe'
Copy-Item -Path $ExePath -Destination $DistExePath -Force

$ChecksumsPath = Join-Path $DistDir 'checksums-sha256.txt'
$hashLines = @(
    (Get-FileHash -Path $ZipPath -Algorithm SHA256),
    (Get-FileHash -Path $DistExePath -Algorithm SHA256)
) | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), (Split-Path -Leaf $_.Path) }
[System.IO.File]::WriteAllLines($ChecksumsPath, $hashLines)

Remove-Item -Path $StageDir -Recurse -Force

Write-Host "Packaged release artifacts in ${DistDir}:"
Get-ChildItem $DistDir | Format-Table Name, Length -AutoSize
