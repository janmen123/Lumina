$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path $compiler)) { throw '.NET Framework compiler was not found.' }
$arguments = @('/nologo','/target:exe','/langversion:5','/codepage:65001','/out:..\CoreTests.exe')
foreach ($ref in @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')) {
    $arguments += ('/reference:' + (Join-Path $framework $ref))
}
$arguments += 'CoreTests.cs'
$arguments += '..\src\NeteaseMusicHelper.cs'
$arguments += '/reference:..\Interop.UIAutomationClient.dll'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& '..\CoreTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
