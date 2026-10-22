[CmdletBinding()]
param([Parameter(Mandatory)][string]$UnityEditor, [ValidateSet("EditMode", "PlayMode")][string]$Platform = "EditMode")
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\unity"
$evidence = Join-Path $PSScriptRoot "..\artifacts\test-results"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
# Tracked scene YAML is not guaranteed to match the generator until Unity
# serializes it. Test the real current-source scene, not an older artifact.
& $UnityEditor -batchmode -quit -projectPath $project -executeMethod "PharmaLabVR.Editor.BuildPipelineEntry.AllAssets" -logFile (Join-Path $evidence "GenerateAssets.log")
if ($LASTEXITCODE -ne 0) { throw "Scene generation failed (exit code $LASTEXITCODE). Tests were not run." }
& $UnityEditor -batchmode -projectPath $project -runTests -testPlatform $Platform -testResults (Join-Path $evidence "$Platform.xml") -logFile (Join-Path $evidence "$Platform.log")
exit $LASTEXITCODE
