[CmdletBinding()]
param(
    [switch]$AllowMissingUnity,
    [string]$JsonOut = "docs/evidence/environment.json"
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = if ($env:PLV_PYTHON) {
    $env:PLV_PYTHON
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    (Get-Command python).Source
} elseif (Get-Command py -ErrorAction SilentlyContinue) {
    (Get-Command py).Source
} else {
    throw "Python 3.11+ not found. Set PLV_PYTHON to an absolute interpreter path."
}

$arguments = @((Join-Path $scriptRoot "doctor.py"), "--json-out", $JsonOut)
if ($AllowMissingUnity) {
    $arguments += "--allow-missing-unity"
}

& $python @arguments
exit $LASTEXITCODE
