$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$root=Split-Path -Parent $PSScriptRoot
$lines=New-Object Collections.Generic.List[string]
$lines.Add('Lumina v'+[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root 'Lumina.exe')).FileVersion+' Windows check')
$lines.Add('OS: '+[Environment]::OSVersion)
$lines.Add('64-bit OS: '+[Environment]::Is64BitOperatingSystem)
foreach($screen in [Windows.Forms.Screen]::AllScreens){$lines.Add('Display: '+$screen.DeviceName+' Primary='+$screen.Primary+' Bounds='+$screen.Bounds+' WorkingArea='+$screen.WorkingArea)}
$process=Start-Process -FilePath (Join-Path $root 'Lumina.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
$lines.Add('WPF self-test exit code: '+$process.ExitCode)
$result=Join-Path $root 'self-test-result.txt'
if(Test-Path $result){$lines.Add((Get-Content -Raw -LiteralPath $result))}
$lines.Add('Manual acceptance still required: third-party app focus/minimize, elevated apps, true fullscreen and multiple monitors, Explorer restart and shell recovery, 100/125/150/200% scaling, tray/orb interactions, real Wi-Fi/Bluetooth/audio/brightness, and long-running performance.')
$destination=Join-Path $root 'WindowsCheck-result.txt'
$lines | Set-Content -LiteralPath $destination -Encoding UTF8
Write-Host ('Saved: '+$destination)

if ($process.ExitCode -ne 0) { throw 'Windows self-test failed; see WindowsCheck-result.txt.' }
