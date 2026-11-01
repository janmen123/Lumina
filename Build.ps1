param([string]$OutputPath = 'Lumina.exe')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path $compiler)) { throw '.NET Framework compiler was not found.' }
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xml.Linq.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')
New-Item -ItemType Directory -Force -Path 'build-output' | Out-Null
$versionSource = Get-Content -Raw -LiteralPath 'src\AppInfo.cs'
if ($versionSource -notmatch 'Version\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'AppInfo version is missing' }
$assemblyVersion = $Matches[1] + '.0'
$manifest = (Get-Content -Raw -LiteralPath 'src\app.manifest').Replace('$LuminaAssemblyVersion$', $assemblyVersion)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'build-output\app.manifest'), $manifest, (New-Object Text.UTF8Encoding($false)))
$arguments = @('/nologo','/target:winexe','/optimize+','/win32icon:src\Lumina.ico','/utf8output','/codepage:65001','/out:build-output\Lumina.exe','/win32manifest:build-output\app.manifest')
foreach ($ref in $refs) { $arguments += ('/reference:' + (Join-Path $framework $ref)) }
foreach ($ref in @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','UIAutomationClient.dll','UIAutomationTypes.dll')) {
    $path = Join-Path $framework ('WPF\' + $ref)
    if (-not (Test-Path $path)) { $path = Join-Path $framework $ref }
    $arguments += ('/reference:' + $path)
}
$arguments += '/link:verification\build-refs\Interop.UIAutomationClient.dll'
$arguments += '/resource:assets\support\c1.dat,GlassBoard.Assets.c1.dat'
$arguments += '/resource:assets\support\c2.dat,GlassBoard.Assets.c2.dat'
$arguments += '/resource:assets\support\c3.dat,GlassBoard.Assets.c3.dat'
$arguments += '/resource:src\SystemControl.ps1,GlassBoard.SystemControl.ps1'
$arguments += @(Get-ChildItem -LiteralPath 'src' -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close Lumina before rebuilding.' }
if (-not [IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $PSScriptRoot $OutputPath }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
if ($OutputPath -ne (Join-Path $PSScriptRoot 'build-output\Lumina.exe')) { Copy-Item -LiteralPath 'build-output\Lumina.exe' -Destination $OutputPath -Force }
if (($OutputPath + '.config') -ne (Join-Path $PSScriptRoot 'Lumina.exe.config')) {
    Copy-Item -LiteralPath 'Lumina.exe.config' -Destination ($OutputPath + '.config') -Force
}
Write-Host ('Built Lumina ' + $assemblyVersion + ': ' + $OutputPath)

