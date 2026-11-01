# Lumina control center only. Uses Windows PowerShell, WinRT and host native
# helpers. No media sessions, lyrics engine, downloaded runtime or network client.
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$pipeInput=New-Object IO.StreamReader([Console]::OpenStandardInput(),(New-Object Text.UTF8Encoding($false,$true)))
$pipeOutput=New-Object IO.StreamWriter([Console]::OpenStandardOutput(),(New-Object Text.UTF8Encoding($false)))
$pipeOutput.AutoFlush=$true
[Console]::SetIn($pipeInput)
[Console]::SetOut($pipeOutput)
$script:controlsReady=$false
$script:bluetoothReady=$false
function Resolve-ControlType($name,$assembly) {
    foreach($candidate in @($assembly,'Windows')) {
        try { $type=[Type]::GetType(($name+', '+$candidate+', ContentType=WindowsRuntime'),$true);if($null -ne $type){return $type} } catch {}
    }
    throw ('Windows 类型不可用：'+$name)
}
function Initialize-Async {
    if($null -ne $script:asTask){return}
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $script:asTask=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } | Select-Object -First 1
    if($null -eq $script:asTask){throw 'Windows async support is unavailable'}
}
function Initialize-Controls {
    if($script:controlsReady){return}
    Initialize-Async
    $script:radioType=Resolve-ControlType 'Windows.Devices.Radios.Radio' 'Windows.System.Devices'
    $script:radioStateType=Resolve-ControlType 'Windows.Devices.Radios.RadioState' 'Windows.System.Devices'
    $script:radioAccessType=Resolve-ControlType 'Windows.Devices.Radios.RadioAccessStatus' 'Windows.System.Devices'
    $script:radioListType=([type]'System.Collections.Generic.IReadOnlyList`1').MakeGenericType($script:radioType)
    $script:controlsReady=$true
}
function Initialize-Bluetooth {
    if($script:bluetoothReady){return}
    Initialize-Async
    $script:deviceType=Resolve-ControlType 'Windows.Devices.Enumeration.DeviceInformation' 'Windows.Devices.Enumeration'
    $script:deviceListType=Resolve-ControlType 'Windows.Devices.Enumeration.DeviceInformationCollection' 'Windows.Devices.Enumeration'
    $script:deviceKindType=Resolve-ControlType 'Windows.Devices.Enumeration.DeviceInformationKind' 'Windows.Devices.Enumeration'
    $script:unpairType=Resolve-ControlType 'Windows.Devices.Enumeration.DeviceUnpairingResult' 'Windows.Devices.Enumeration'
    $script:bluetoothReady=$true
}
function Await-Control($operation,$resultType) {
    $task=$script:asTask.MakeGenericMethod($resultType).Invoke($null,@($operation))
    if(-not $task.Wait(4000)){throw 'Windows control operation timed out'}
    # Keep an empty/singleton WinRT collection from becoming null/a scalar.
    return ,$task.Result
}
function Read-Radios {
    Initialize-Controls
    $radios=Await-Control ($script:radioType::GetRadiosAsync()) $script:radioListType
    return @($radios | Where-Object {$_.Kind.ToString() -eq 'WiFi' -or $_.Kind.ToString() -eq 'Bluetooth'})
}
function Read-State {
    $state=@{Audio=$null;Brightness=$null;Radios=@();Errors=@()}
    try{$state.Audio=[Lumina.NativeAudio]::State()}catch{$state.Errors+=('音频：'+$_.Exception.GetBaseException().Message)}
    try{$state.Brightness=[Lumina.NativeBrightness]::Read()}catch{$state.Errors+=('亮度：'+$_.Exception.GetBaseException().Message)}
    try{$state.Radios=@(Read-Radios | ForEach-Object {@{Kind=$_.Kind.ToString();Name=$_.Name;Enabled=$_.State.ToString() -eq 'On';Available=$_.State.ToString() -ne 'Disabled'}})}catch{$state.Errors+=('无线：'+$_.Exception.GetBaseException().Message)}
    # Native fallbacks also distinguish missing hardware from WinRT/architecture failures.
    if(@($state.Radios | Where-Object {$_.Kind -eq 'WiFi'}).Count -eq 0){
        try{$state.Radios+=@([Lumina.NativeWireless]::Radios())}catch{$state.Errors+=('Wi-Fi：'+$_.Exception.GetBaseException().Message)}
    }
    if(@($state.Radios | Where-Object {$_.Kind -eq 'Bluetooth'}).Count -eq 0){
        try{$state.Radios+=@([Lumina.NativeBluetooth]::Radios())}catch{$state.Errors+=('蓝牙：'+$_.Exception.GetBaseException().Message)}
    }
    return @{System=$state}
}
function Set-Radio($kind,$enabled) {
    if($kind -ne 'WiFi' -and $kind -ne 'Bluetooth'){throw 'Invalid radio kind'}
    $radios=@()
    try{Initialize-Controls;$radios=@(Read-Radios | Where-Object {$_.Kind.ToString() -eq $kind})}catch{if($kind -ne 'WiFi'){throw}}
    if($radios.Count -eq 0){
        if($kind -eq 'WiFi'){[Lumina.NativeWireless]::SetRadio([bool]$enabled);return}
        throw '蓝牙开关未由系统接口提供，请打开 Windows 蓝牙设置。'
    }
    $access=Await-Control ($script:radioType::RequestAccessAsync()) $script:radioAccessType
    if($access.ToString() -ne 'Allowed'){throw 'Windows 未授权无线开关，请在系统设置中操作。'}
    $target=[Enum]::Parse($script:radioStateType,($(if($enabled){'On'}else{'Off'})))
    foreach($radio in $radios){
        $result=Await-Control ($radio.SetStateAsync($target)) $script:radioAccessType
        if($result.ToString() -ne 'Allowed'){throw ('无线状态修改被 Windows 拒绝：'+$result)}
    }
}
function Read-Bluetooth {
    Initialize-Bluetooth
    $query='System.Devices.Aep.ProtocolId:="{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}" AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True'
    $kind=[Enum]::Parse($script:deviceKindType,'AssociationEndpoint')
    $devices=Await-Control ($script:deviceType::FindAllAsync($query,[string[]]@('System.Devices.Aep.IsConnected'),$kind)) $script:deviceListType
    return @{Devices=@($devices | ForEach-Object {@{Id=$_.Id;Name=$_.Name;Paired=$_.Pairing.IsPaired;Connected=$_.Properties['System.Devices.Aep.IsConnected'] -eq $true}})}
}
function Dispatch-Control($request) {
    switch($request.Action){
        'state' {return Read-State}
        'radio' {Set-Radio $request.Text $request.Enabled;return @{}}
        'networks' {return @{Networks=@([Lumina.NativeWireless]::Networks($request.Enabled))}}
        'connect' {[Lumina.NativeWireless]::Connect($request.Adapter,$request.Text,$request.Profile,$request.Password,$request.Auth,$request.Cipher);return @{}}
        'disconnect' {[Lumina.NativeWireless]::Disconnect($request.Adapter);return @{}}
        'bluetooth' {try{return Read-Bluetooth}catch{$reason=$_.Exception.GetBaseException().Message;return @{Devices=@([Lumina.NativeBluetooth]::Devices());Warning=('已使用兼容列表；部分低功耗设备请在系统设置中查看。'+$reason)}}}
        'unpair' {
            Initialize-Bluetooth
            $device=Await-Control ($script:deviceType::CreateFromIdAsync($request.Key)) $script:deviceType
            $result=Await-Control ($device.Pairing.UnpairAsync()) $script:unpairType
            if($result.Status.ToString() -ne 'Unpaired' -and $result.Status.ToString() -ne 'AlreadyUnpaired'){throw ('取消配对未完成：'+$result.Status)}
            return @{}
        }
        'volume' {[Lumina.NativeAudio]::Set([double]$request.Value,$null);return @{}}
        'mute' {[Lumina.NativeAudio]::Set($null,[bool]$request.Enabled);return @{}}
        'output' {[Lumina.NativeAudio]::Output($request.Key);return @{}}
        'brightness' {[Lumina.NativeBrightness]::Set([double]$request.Value);return @{}}
        default {throw 'Unknown control action'}
    }
}
while($null -ne ($line=[Console]::ReadLine())){
    $request=$null;$id=0
    try{
        if($line.Length -gt 131072){throw 'Request too large'}
        $request=ConvertFrom-Json -InputObject $line
        if($null -eq $request -or $request.Version -ne 1 -or [int]$request.Id -lt 1){throw 'Invalid protocol version or request id'}
        $id=[int]$request.Id
        $result=Dispatch-Control $request
        $reply=@{Version=1;Id=$id;Ok=$true;Result=$result}
    }catch{$reply=@{Version=1;Id=$id;Ok=$false;Error=$_.Exception.GetBaseException().Message}}
    finally{if($null -ne $request -and $request.PSObject.Properties['Password']){$request.Password=''}}
    [Console]::WriteLine((ConvertTo-Json -InputObject $reply -Depth 12 -Compress))
}
