[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"

# Exercise the real local Unity runner with a fake executable in isolation.
# This validates ONLY CLI/report rules. No Editor, Player or XR is launched.
$root = Join-Path ([System.IO.Path]::GetTempPath()) (
    "plv-unity-runner-" + [guid]::NewGuid().ToString("N"))
$fake = Join-Path $root "fake-unity.sh"
$isolatedRunner = Join-Path $root "scripts/test-unity.ps1"
$sourceRunner = Join-Path $PSScriptRoot "../../scripts/test-unity.ps1"
New-Item -ItemType Directory -Force -Path (Join-Path $root "scripts") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "unity") | Out-Null
Copy-Item -LiteralPath $sourceRunner -Destination $isolatedRunner
$fakeText = @'
#!/usr/bin/env bash
set -euo pipefail
mode="$PLV_FAKE_UNITY_MODE"
for arg in "$@"; do
  if [[ "$arg" == "-executeMethod" ]]; then
    if [[ "$mode" == "generation-fail" ]]; then exit 23; fi
    exit 0
  fi
done
result=""
previous=""
for arg in "$@"; do
  if [[ "$previous" == "-testResults" ]]; then result="$arg"; break; fi
  previous="$arg"
done
if [[ -z "$result" ]]; then exit 26; fi
if [[ "$mode" == "missing" ]]; then exit 0; fi
if [[ "$mode" == "bad-xml" ]]; then echo "<test-run" > "$result"; exit 0; fi
name="M3_ExplicitNotebookSaveAndReviewNeedsRecordedNativePHAndDurableSave"
root_status="Passed"
case_status="Passed"
total=1
passed=1
failed=0
skipped=0
inconclusive=0
case "$mode" in
  zero) total=0; passed=0 ;;
  failed) passed=0; failed=1; root_status="Failed"; case_status="Failed" ;;
  skipped) passed=0; skipped=1; root_status="Skipped"; case_status="Skipped" ;;
  inconclusive) passed=0; inconclusive=1; root_status="Inconclusive"; case_status="Inconclusive" ;;
  wrong-test|edit-pass) name="UnrelatedSyntheticTest" ;;
  valid|nonzero) ;;
  *) exit 30 ;;
esac
cat > "$result" <<XML
<?xml version="1.0" encoding="utf-8"?>
<test-run result="$root_status" total="$total" passed="$passed" failed="$failed" skipped="$skipped" inconclusive="$inconclusive">
  <test-suite result="$root_status">
    <test-case fullname="PharmaLabVR.Tests.PlayMode.NativeRuntimeSmokeTests.$name" result="$case_status" />
  </test-suite>
</test-run>
XML
if [[ "$mode" == "nonzero" ]]; then exit 41; fi
'@
# PowerShell here-strings from a CRLF checkout can retain carriage
# returns even when invoked on Linux. Normalize the fake bash shebang:
# /usr/bin/env must see "bash", never "bash" followed by CR.
$lf = ([char]10).ToString()
$crlf = ([char]13).ToString() + $lf
$fakeText = $fakeText.Replace($crlf, $lf)
[System.IO.File]::WriteAllText($fake, $fakeText,
    (New-Object System.Text.UTF8Encoding($false)))
& chmod +x $fake
if ($LASTEXITCODE -ne 0) { throw "Fake Unity executable could not be prepared." }

$cases = @(
    @{ Mode = "missing"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "zero"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "failed"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "skipped"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "inconclusive"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "bad-xml"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "wrong-test"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "generation-fail"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "nonzero"; Platform = "PlayMode"; Pass = $false },
    @{ Mode = "valid"; Platform = "PlayMode"; Pass = $true },
    @{ Mode = "edit-pass"; Platform = "EditMode"; Pass = $true }
)
try {
    foreach ($case in $cases) {
        $env:PLV_FAKE_UNITY_MODE = $case.Mode
        $output = (& pwsh -NoProfile -File $isolatedRunner -UnityEditor $fake -Platform $case.Platform 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
        $passed = $exitCode -eq 0
        if ($passed -ne $case.Pass) {
            throw "Fake Unity runner regression ($($case.Mode), $($case.Platform)): expected success=$($case.Pass), actual exit=$exitCode. Child output: $output"
        }
    }
    Write-Host "Local Unity runner contract: $($cases.Count) fake-editor cases passed (NOT Unity)."
}
finally {
    Remove-Item Env:PLV_FAKE_UNITY_MODE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction SilentlyContinue
}
