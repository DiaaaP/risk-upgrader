$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$build = Join-Path $root 'build'
$compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) { $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" }
New-Item -ItemType Directory -Path $build -Force | Out-Null
& $compiler /nologo /target:exe ('/out:' + (Join-Path $build 'UpgradeMathTests.exe')) `
    (Join-Path $root 'UpgradeMath.cs') (Join-Path $root 'tests\UpgradeMathTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed to compile.' }
& (Join-Path $build 'UpgradeMathTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

$gameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2'
$managed = Join-Path $gameDir 'Risk of Rain 2_Data\Managed'
$hlapi = Join-Path $managed 'com.unity.multiplayer-hlapi.Runtime.dll'
$unityCore = Join-Path $managed 'UnityEngine.CoreModule.dll'
$netstandard = Join-Path $managed 'netstandard.dll'
& $compiler /nologo /target:exe ('/out:' + (Join-Path $build 'MultiplayerProtocolTests.exe')) `
    ('/reference:' + $hlapi) ('/reference:' + $unityCore) ('/reference:' + $netstandard) `
    (Join-Path $root 'MultiplayerProtocol.cs') (Join-Path $root 'tests\MultiplayerProtocolTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed to compile.' }
Copy-Item -LiteralPath $hlapi -Destination $build -Force
Copy-Item -LiteralPath $unityCore -Destination $build -Force
Copy-Item -LiteralPath $netstandard -Destination $build -Force
& (Join-Path $build 'MultiplayerProtocolTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed.' }
