param([string]$Binary)
$ErrorActionPreference = 'Stop'
# Compatibility entry point. The current runner tests the compiled production assembly.
& (Join-Path $PSScriptRoot 'RunRegression.ps1') -Binary $Binary
