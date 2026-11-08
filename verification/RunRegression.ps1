param([string]$Binary)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Binary) { $Binary = Join-Path $root 'Lumina.exe' }
$Binary = [IO.Path]::GetFullPath($Binary)
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$runner = Join-Path (Split-Path -Parent $Binary) 'Lumina.Regression.exe'
$arguments = @('/nologo','/target:exe','/langversion:5','/codepage:65001','/main:ReliabilityRegression',('/out:' + $runner),('/reference:' + $Binary))
foreach ($name in @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')) {
    $arguments += '/reference:' + (Join-Path $framework $name)
}
foreach ($name in @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')) {
    $arguments += '/reference:' + (Join-Path $framework ('WPF\' + $name))
}
foreach ($name in @('ReliabilityRegression.cs','PersistenceTests.cs','DockPointerTests.cs','DockBehaviorTests.cs')) {
    $arguments += Join-Path $PSScriptRoot $name
}
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Regression compilation failed.' }
& $runner
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }

# Exercise backup export/restore and group undo against the same production binary.
$deepRunner = Join-Path (Split-Path -Parent $Binary) 'Lumina.DeepRegression.exe'
$deepArgs = @('/nologo','/target:exe','/langversion:5','/codepage:65001',('/out:' + $deepRunner),('/reference:' + $Binary),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Runtime.Serialization.dll',(Join-Path $PSScriptRoot 'DeepAuditRegression.cs'))
& (Join-Path $framework 'csc.exe') @deepArgs
if ($LASTEXITCODE -ne 0) { throw 'Deep regression compilation failed.' }
& $deepRunner
if ($LASTEXITCODE -ne 0) { throw 'Backup/group regression checks failed.' }
