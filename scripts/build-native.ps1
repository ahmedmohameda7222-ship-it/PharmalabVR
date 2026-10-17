[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
cmake -S . -B build/native -DBUILD_TESTING=ON
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
cmake --build build/native --config $Configuration --parallel 2
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
ctest --test-dir build/native -C $Configuration --output-on-failure
exit $LASTEXITCODE
