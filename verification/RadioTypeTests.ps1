param([Parameter(Mandatory=$true)][string]$Binary)
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($Binary))
$reader=New-Object IO.StreamReader($assembly.GetManifestResourceStream('GlassBoard.SystemControl.ps1'))
try{$source=$reader.ReadToEnd()}finally{$reader.Dispose()}
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Embedded control script has syntax errors'}
foreach($name in @('Resolve-ControlType','Initialize-Async','Initialize-Controls')){
    $function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$true)
    if($null -eq $function){throw ('Missing function: '+$name)}
    . ([scriptblock]::Create($function.Extent.Text))
}
Initialize-Controls
if(-not $script:radioListType.IsGenericType -or $script:radioListType.GetGenericArguments()[0] -ne $script:radioType){throw 'Radio collection type does not match the WinRT async result'}
Write-Output 'PASS: embedded radio initialization and WinRT generic result type; no hardware state changed.'
