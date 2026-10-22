$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1')

$modVersion = '0.4.0'
$pluginName = 'RiskUpgrader'
$pluginDll = Join-Path $root 'build\RiskUpgrader.dll'
$bepInExVersion = '5.4.23.5'
$bepInExSha256 = '82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4'
$bepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepInExVersion/BepInEx_win_x64_$bepInExVersion.zip"
$bepInExLicenseUrl = "https://raw.githubusercontent.com/BepInEx/BepInEx/v$bepInExVersion/LICENSE"

$cache = Join-Path ([IO.Path]::GetTempPath()) "SanaBepInEx-$bepInExVersion"
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$bepInExZip = Join-Path $cache "BepInEx_win_x64_$bepInExVersion.zip"
if (-not (Test-Path -LiteralPath $bepInExZip)) {
    Invoke-WebRequest -Uri $bepInExUrl -OutFile $bepInExZip
}
$actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $bepInExZip).Hash.ToLowerInvariant()
if ($actualSha256 -ne $bepInExSha256) {
    throw "BepInEx SHA256 mismatch. Expected $bepInExSha256, got $actualSha256"
}

$bepInExLicense = Join-Path $cache 'BepInEx-LICENSE.txt'
if (-not (Test-Path -LiteralPath $bepInExLicense)) {
    Invoke-WebRequest -Uri $bepInExLicenseUrl -OutFile $bepInExLicense
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$stage = Join-Path $dist ("oneclick-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $stage -Force

$pluginDir = Join-Path $stage "BepInEx\plugins\$pluginName"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination $pluginDir -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $pluginDir -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $pluginDir -Force

$thirdParty = Join-Path $stage 'THIRD-PARTY-NOTICES'
New-Item -ItemType Directory -Path $thirdParty -Force | Out-Null
Copy-Item -LiteralPath $bepInExLicense -Destination (Join-Path $thirdParty 'BepInEx-LICENSE.txt') -Force
@"
BepInEx $bepInExVersion (official Windows x64 release)
Source: https://github.com/BepInEx/BepInEx/tree/v$bepInExVersion
Release: https://github.com/BepInEx/BepInEx/releases/tag/v$bepInExVersion
Original archive SHA256: $bepInExSha256
The BepInEx files are redistributed unchanged from the official release archive.
"@ | Set-Content -LiteralPath (Join-Path $thirdParty 'BepInEx-SOURCE.txt') -Encoding utf8

@"
RISK UPGRADER $modVersion - БЫСТРАЯ УСТАНОВКА

1. Закрой Risk of Rain 2.
2. Открой Steam -> Risk of Rain 2 -> Управление -> Просмотреть локальные файлы.
3. Распакуй СОДЕРЖИМОЕ этого архива прямо в папку игры рядом с Risk of Rain 2.exe.
4. Запусти игру обычным способом через Steam. Отдельно запускать BepInEx не нужно.
5. В забеге нажми F7.

Архив уже содержит официальный BepInEx $bepInExVersion для Windows x64 и DLL мода.
Для удаления мода удали BepInEx\plugins\$pluginName.
"@ | Set-Content -LiteralPath (Join-Path $stage 'README-INSTALL-RU.txt') -Encoding utf8

$output = Join-Path $dist "RiskUpgrader-$modVersion-Windows-OneClick.zip"
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $output, [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Host "One-click package: $output" -ForegroundColor Green
