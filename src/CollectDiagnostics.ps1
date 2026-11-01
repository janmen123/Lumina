param([string]$Destination)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Destination) {
    $Destination = Join-Path ([Environment]::GetFolderPath('Desktop')) ('Lumina_Diagnostics_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.zip')
}
$Destination = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $Destination) { throw 'Destination already exists; choose another file.' }
$process = Start-Process -FilePath (Join-Path $root 'Lumina.exe') -ArgumentList @('--export-diagnostics', ('"' + $Destination + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $Destination)) { throw 'Diagnostic export failed.' }
Write-Host ('Saved redacted diagnostics: ' + $Destination)
Write-Host 'Layouts and notes are excluded. Review free-form error text before sharing.'
