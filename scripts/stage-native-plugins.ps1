[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repository = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$windowsSource = Join-Path $repository "build\native\native\$Configuration\pharmalab_core.dll"
$androidSource = Join-Path $repository "build\android-arm64\native\libpharmalab_core.so"
$windowsDestination = Join-Path $repository "unity\Assets\Plugins\x86_64\pharmalab_core.dll"
$androidDestination = Join-Path $repository "unity\Assets\Plugins\Android\arm64-v8a\libpharmalab_core.so"

foreach ($source in @($windowsSource, $androidSource)) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Native plugin not found: $source" }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $windowsDestination) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $androidDestination) | Out-Null
Copy-Item -LiteralPath $windowsSource -Destination $windowsDestination -Force
Copy-Item -LiteralPath $androidSource -Destination $androidDestination -Force

Get-FileHash -Algorithm SHA256 -LiteralPath $windowsDestination, $androidDestination |
    Select-Object Algorithm, Hash, Path
