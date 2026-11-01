param([string]$Binary)
$ErrorActionPreference='Stop'
if(-not $Binary){$Binary=Join-Path (Split-Path -Parent $PSScriptRoot) 'Lumina.exe'}
$Binary=[IO.Path]::GetFullPath($Binary)
$profile=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Lumina'
function Get-ProfileFingerprint {
    $result=@{}
    if(Test-Path -LiteralPath $profile){
        foreach($file in Get-ChildItem -LiteralPath $profile -Recurse -File){
            $result[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return $result
}
$before=Get-ProfileFingerprint
$test=Start-Process -FilePath $Binary -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
$after=Get-ProfileFingerprint
$changes=@(@($before.Keys)+@($after.Keys) | Sort-Object -Unique | Where-Object {$before[$_] -ne $after[$_]})
$output=Split-Path -Parent $Binary
$isolation=Get-Content -Raw -LiteralPath (Join-Path $output 'test-isolation-result.txt')
$summary=Get-Content -Raw -LiteralPath (Join-Path $output 'self-test-result.txt')
Write-Output $isolation.Trim()
Write-Output $summary.Trim()
Write-Output ('Production files checked: '+$before.Count+'; changed: '+$changes.Count)
if($test.ExitCode -ne 0 -or $isolation -notmatch '^PASS' -or $summary -notmatch '^PASS') {throw 'Self-test failed.'}
if($changes.Count -ne 0){throw 'Production profile changed during verification. Check concurrent activity in the running app before attributing the change to tests.'}
