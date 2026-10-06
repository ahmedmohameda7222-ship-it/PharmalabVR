[CmdletBinding()]
param([Parameter(Mandatory)][string]$UnityEditor, [ValidateSet("EditMode", "PlayMode")][string]$Platform = "EditMode")
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\unity"
$evidence = Join-Path $PSScriptRoot "..\artifacts\test-results"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
& $UnityEditor -batchmode -projectPath $project -runTests -testPlatform $Platform -testResults (Join-Path $evidence "$Platform.xml") -logFile (Join-Path $evidence "$Platform.log")
exit $LASTEXITCODE
