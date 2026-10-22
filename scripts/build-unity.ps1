[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityEditor,
    [ValidateSet("AllAssets", "WindowsDesktop", "WindowsVR", "AndroidVR", "AndroidVRPico")][string]$Target = "AllAssets"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity Editor not found: $UnityEditor" }
$project = Join-Path $PSScriptRoot "..\unity"
$log = Join-Path $PSScriptRoot "..\artifacts\logs\unity-$Target.log"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
& $UnityEditor -batchmode -quit -projectPath $project -executeMethod "PharmaLabVR.Editor.BuildPipelineEntry.$Target" -logFile $log
exit $LASTEXITCODE
