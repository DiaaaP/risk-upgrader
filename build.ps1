param(
    [switch]$Install,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$managed = Join-Path $GameDir 'Risk of Rain 2_Data\Managed'
$bepinex = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'
$build = Join-Path $root 'build'
$output = Join-Path $build 'RiskUpgrader.dll'
$compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) { $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" }
if (-not (Test-Path $compiler)) { throw 'C# compiler not found.' }
New-Item -ItemType Directory -Path $build -Force | Out-Null
$references = @($bepinex, (Join-Path $managed 'RoR2.dll'), (Join-Path $managed 'HGUnityUtils.dll'), (Join-Path $managed 'KinematicCharacterController.dll'), (Join-Path $managed 'com.unity.multiplayer-hlapi.Runtime.dll'), (Join-Path $managed 'UnityEngine.UI.dll'), (Join-Path $managed 'Unity.Addressables.dll'), (Join-Path $managed 'Unity.ResourceManager.dll'))
$references += Get-ChildItem $managed -Filter 'UnityEngine*.dll' -File | ForEach-Object FullName
foreach($name in @('netstandard.dll','System.Runtime.dll')) { $candidate=Join-Path $managed $name; if(Test-Path $candidate){$references += $candidate} }
$arguments = @('/nologo','/target:library',('/out:'+$output))
foreach($reference in ($references | Select-Object -Unique)){ $arguments += '/reference:'+$reference }
$arguments += (Join-Path $root 'UpgradeMath.cs')
$arguments += (Join-Path $root 'UpgraderIconManager.cs')
$arguments += (Join-Path $root 'RiskUpgraderPlugin.cs')
& $compiler @arguments
if($LASTEXITCODE -ne 0 -or -not (Test-Path $output)){throw 'Build failed.'}
Write-Host "Built: $output" -ForegroundColor Green
if($Install){
    if(Get-Process RiskofRain2 -ErrorAction SilentlyContinue){throw 'Risk of Rain 2 is running. Close it before installation.'}
    $destination=Join-Path $GameDir 'BepInEx\plugins\RiskUpgrader'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $installed=Join-Path $destination 'RiskUpgrader.dll'
    if(Test-Path $installed){Copy-Item $installed ($installed+'.'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.bak') -Force}
    Copy-Item $output $installed -Force
    Write-Host "Installed: $installed" -ForegroundColor Green
}
