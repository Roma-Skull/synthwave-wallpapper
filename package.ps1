#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$VersionFile = Join-Path $Root 'VERSION'
if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path $VersionFile) {
        $Version = (Get-Content -Path $VersionFile -Raw).Trim()
    } else {
        $Version = '1.1.0'
    }
}

$CleanVer = $Version.Trim() -replace '^(?:[a-zA-Z0-9_-]+-)?[vV]', ''
$ExePath = Join-Path $Root 'NeonHorizonWallpaper.exe'

& (Join-Path $Root 'build.ps1') -OutExe $ExePath -Version $CleanVer

$DistDir = Join-Path $Root 'dist'
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$PackageName = "NeonHorizonWallpaper-v$CleanVer-windows"
$StageDir = Join-Path $DistDir $PackageName
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

Copy-Item -Path $ExePath -Destination (Join-Path $StageDir 'NeonHorizonWallpaper.exe') -Force
Copy-Item -Path (Join-Path $Root 'README.md') -Destination (Join-Path $StageDir 'README.md') -Force

$ZipPath = Join-Path $DistDir "$PackageName.zip"
Compress-Archive -Path "$StageDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal -Force

$DistWinExePath = Join-Path $DistDir 'NeonHorizonWallpaper-windows.exe'
Copy-Item -Path $ExePath -Destination $DistWinExePath -Force

$ExeHash = (Get-FileHash -Path $DistWinExePath -Algorithm SHA256).Hash.ToLowerInvariant()
$ZipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$ExeSize = (Get-Item $DistWinExePath).Length

$ChecksumsPath = Join-Path $DistDir 'checksums-windows-sha256.txt'
$hashLines = @(
    "$ZipHash  $(Split-Path -Leaf $ZipPath)"
    "$ExeHash  $(Split-Path -Leaf $DistWinExePath)"
)
[System.IO.File]::WriteAllLines($ChecksumsPath, $hashLines)

# OS-scoped release manifest so future multi-OS releases (android, linux, macos, etc.) stay cleanly separated
$ManifestPath = Join-Path $DistDir 'manifest-windows.json'
$manifestObj = [ordered]@{
    schemaVersion = 1
    platform      = 'windows'
    tag           = "windows-v$CleanVer"
    version       = $CleanVer
    asset         = (Split-Path -Leaf $DistWinExePath)
    zipAsset      = (Split-Path -Leaf $ZipPath)
    sha256        = $ExeHash
    sizeBytes     = $ExeSize
    minOs         = 'Windows 10'
    releasedAtUtc = [DateTime]::UtcNow.ToString('o')
}
[System.IO.File]::WriteAllText($ManifestPath, ($manifestObj | ConvertTo-Json -Depth 4), [System.Text.Encoding]::UTF8)

Remove-Item -Path $StageDir -Recurse -Force

Write-Host "Packaged Windows release artifacts in ${DistDir}:"
Get-ChildItem $DistDir | Format-Table Name, Length -AutoSize
