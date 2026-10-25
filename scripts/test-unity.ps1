[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$UnityEditor,
    [ValidateSet("EditMode", "PlayMode")][string]$Platform = "EditMode"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity Editor not found: $UnityEditor"
}
$project = Join-Path $PSScriptRoot "..\unity"
$evidence = Join-Path $PSScriptRoot "..\artifacts\test-results"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$resultFile = Join-Path $evidence "$Platform.xml"
$generationLog = Join-Path $evidence "GenerateAssets.log"
$testLog = Join-Path $evidence "$Platform.log"

# A previous test report must NEVER be mistaken for the current source SHA.
# Only a brand-new report from the Unity process below qualifies as evidence.
if (Test-Path -LiteralPath $resultFile) {
    Remove-Item -LiteralPath $resultFile -Force
}
& $UnityEditor -batchmode -quit -projectPath $project -executeMethod "PharmaLabVR.Editor.BuildPipelineEntry.AllAssets" -logFile $generationLog
if ($LASTEXITCODE -ne 0) {
    throw "Scene generation failed (exit code $LASTEXITCODE). Tests were not run."
}
& $UnityEditor -batchmode -projectPath $project -runTests -testPlatform $Platform -testResults $resultFile -logFile $testLog
if ($LASTEXITCODE -ne 0) {
    throw "Unity $Platform returned failure (exit code $LASTEXITCODE). See $testLog"
}
if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
    throw "Test report unavailable: Unity $Platform did not write a new XML file."
}

# Exit code alone is insufficient: missing, invalid, empty, failed, skipped
# and inconclusive suites must all refuse green acceptance.
try {
    [xml]$report = Get-Content -LiteralPath $resultFile -Raw -ErrorAction Stop
} catch {
    throw "Test report unavailable: invalid Unity NUnit XML. $($_.Exception.Message)"
}
$root = $report.SelectSingleNode("/test-run")
if ($null -eq $root) {
    throw "Test report unavailable: missing NUnit test-run root."
}
$counts = @{}
foreach ($field in @("total", "passed", "failed", "skipped", "inconclusive")) {
    $value = 0
    if (-not [int]::TryParse($root.GetAttribute($field), [ref]$value) -or
        $value -lt 0) {
        throw "Test report unavailable: invalid $field count."
    }
    $counts[$field] = $value
}
if ($counts["total"] -lt 1 -or $counts["passed"] -ne $counts["total"] -or
    $counts["failed"] -ne 0 -or $counts["skipped"] -ne 0 -or
    $counts["inconclusive"] -ne 0 -or
    $root.GetAttribute("result") -ne "Passed") {
    throw "Unity $Platform did not pass every scheduled test. Total=$($counts['total']); Passed=$($counts['passed']); Failed=$($counts['failed']); Skipped=$($counts['skipped']); Inconclusive=$($counts['inconclusive'])."
}
# This milestone is NOT verified by a PlayMode suite which ran only
# unrelated smoke tests or omitted the real pinned Native learner journey.
if ($Platform -eq "PlayMode") {
    $requiredName = "M3_ExplicitNotebookSaveAndReviewNeedsRecordedNativePHAndDurableSave"
    $requiredPassed = $false
    foreach ($case in $report.SelectNodes("//test-case[@result='Passed']")) {
        if ($case.GetAttribute("fullname").Contains($requiredName)) {
            $requiredPassed = $true
            break
        }
    }
    if (-not $requiredPassed) {
        throw "Unity PlayMode lacked passing required M3_ExplicitNotebookSaveAndReviewNeedsRecordedNativePHAndDurableSave evidence."
    }
}
Write-Host "Unity $Platform confirmed $($counts['passed']) executed tests; report: $resultFile"
exit 0
