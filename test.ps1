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
