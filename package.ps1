$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'RiskUpgrader'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $root 'build\RiskUpgrader.dll') $stage -Force
Copy-Item (Join-Path $root 'README.md') $stage -Force
Copy-Item (Join-Path $root 'LICENSE') $stage -Force
$zip = Join-Path $dist 'RiskUpgrader-0.4.0.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Package: $zip" -ForegroundColor Green
