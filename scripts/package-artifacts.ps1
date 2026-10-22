[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$Revision
)
$ErrorActionPreference = "Stop"
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) { throw "Artifact source is not a directory: $Source" }
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$manifest = @{
    schemaVersion = 1
    revision = $Revision
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    files = @()
}
Get-ChildItem -LiteralPath $sourcePath -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($sourcePath, $_.FullName).Replace('\', '/')
    $manifest.files += @{
        path = $relative
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
if ($manifest.files.Count -eq 0) { throw "No real artifacts found under $sourcePath" }
$manifestPath = Join-Path $Destination "artifact-manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
$archivePath = Join-Path $Destination "PharmaLabVR-$Revision.zip"
Compress-Archive -Path (Join-Path $sourcePath '*') -DestinationPath $archivePath -Force
Write-Output $manifestPath
Write-Output $archivePath
