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
