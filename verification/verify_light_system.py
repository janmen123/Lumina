"""Exercise the shipped script's actual JSON loop and routing with fake devices,
then load the final EXE's embedded resource in a clean PowerShell process.
Linux cannot validate Windows COM/WinRT hardware or Windows PowerShell 5.1.
Usage: python3 verification/verify_light_system.py /path/to/pwsh
"""
from pathlib import Path
import subprocess,sys,tempfile,json
root=Path(__file__).resolve().parents[1]
pwsh=str(Path(sys.argv[1]).resolve())
source=(root/'src/SystemControl.ps1').read_text(encoding='utf-8-sig')
fake=r'''
Add-Type -TypeDefinition @'
using System;using System.Collections.Generic;
namespace GlassBoard {
 public class Output{public string Id="out1",Name="中文扬声器";public bool Selected=true;}
 public class AudioData{public double Volume;public bool Muted;public Output[] Outputs=new[]{new Output()};}
 public static class NativeAudio{
  static double volume=.3;static bool muted;
  public static AudioData State(){return new AudioData{Volume=volume,Muted=muted};}
  public static void Set(double? value,bool? mute){if(value.HasValue)volume=value.Value;if(mute.HasValue)muted=mute.Value;}
  public static void Output(string id){if(id!="out1")throw new Exception("device unavailable");}
 }
 public static class NativeBluetooth {public static object[] Radios(){return new object[0];}public static object[] Devices(){return new object[0];}}
 public static class TrayAccess {public static object[] Read(){return new object[0];}public static void Invoke(string key){if(key!="icon")throw new Exception("stale icon");}public static void OpenOverflow(){}}
 public static class NativeBrightness{static double value=75;public static double? Read(){return value;}public static void Set(double n){value=n;}}
 public class Network{public string Name="测试网络",Profile="saved",Adapter="adapter";public int Signal=88,Auth=7,Cipher=4;public bool Connected,Secure=true;}
 public static class NativeWireless{
  static bool connected;public static Network[] Networks(bool scan){return new[]{new Network{Connected=connected}};}
  public static void Connect(string a,string s,string p,string password,int auth,int cipher){if(a!="adapter")throw new Exception("adapter unavailable");connected=true;}
  public static void Disconnect(string a){connected=false;}
 }
}
public enum TestState {On,Off}
public enum TestAccess {Allowed,Denied}
public class TestRadio {
 public string Kind,Name;public TestState State=TestState.Off;
 public TestAccess SetStateAsync(TestState state){State=state;return TestAccess.Allowed;}
}
public static class TestRadios {
 static TestRadio[] radios={new TestRadio{Kind="WiFi",Name="无线网卡"},new TestRadio{Kind="Bluetooth",Name="蓝牙"}};
 public static TestRadio[] GetRadiosAsync(){return radios;}
 public static TestAccess RequestAccessAsync(){return TestAccess.Allowed;}
}
'@
'''
overrides=r'''
function Initialize-Controls {
 $script:radioType=[TestRadios];$script:radioStateType=[TestState];$script:radioAccessType=[TestAccess];$script:radioListType=[TestRadio[]]
}
function Await-Control($operation,$resultType){return ,$operation}
'''
requests=[{'Action':'state'}, {'Action':'volume','Value':.81}, {'Action':'mute','Enabled':True},
          {'Action':'brightness','Value':46}, {'Action':'radio','Text':'WiFi','Enabled':True}, {'Action':'state'},
          {'Action':'networks','Enabled':True}, {'Action':'connect','Adapter':'adapter','Text':'测试网络','Profile':'saved','Password':'DO_NOT_LOG_PASSWORD','Auth':7,'Cipher':4},
          {'Action':'networks'}, {'Action':'disconnect','Adapter':'adapter'}, {'Action':'networks'},
          {'Action':'output','Key':'out1'}, {'Action':'output','Key':'invalid'},
          {'Action':'media'}, {'Action':'lyrics'}, {'Action':'transport'}, {'Action':'radio','Text':'invalid'},
          {'Action':'state'}]
for i,request in enumerate(requests,1):request.update(Version=1,Id=i)
payload='\n'.join(json.dumps(r,ensure_ascii=False) for r in requests)+'\n{broken\n'+json.dumps({'Version':2,'Id':99,'Action':'state'})+'\n'
def run(script,payload):
    with tempfile.TemporaryDirectory(prefix='gb17-system-') as temp:
        path=Path(temp)/'probe.ps1';path.write_text(script)
        r=subprocess.run([pwsh,'-NoLogo','-NoProfile','-NonInteractive','-File',str(path)],input=payload,text=True,capture_output=True,timeout=45)
        if r.returncode:raise AssertionError(r.stderr)
        assert 'DO_NOT_LOG_PASSWORD' not in r.stdout+r.stderr
        return [json.loads(line) for line in r.stdout.splitlines()]
loop=source.index('while($null -ne ($line=')
replies=run(fake+source[:loop]+overrides+source[loop:],payload)
assert len(replies)==len(requests)+2
for i,r in enumerate(replies[:len(requests)],1):assert r['Version']==1 and r['Id']==i
state=replies[5]['Result']['System']
assert state['Audio']['Volume']==.81 and state['Audio']['Muted'] is True
assert state['Audio']['Outputs'][0]['Name']=='中文扬声器' and state['Brightness']==46
assert state['Radios'][0]['Enabled'] is True and state['Errors']==[]
assert replies[6]['Result']['Networks'][0]['Name']=='测试网络'
assert replies[8]['Result']['Networks'][0]['Connected'] is True
assert replies[10]['Result']['Networks'][0]['Connected'] is False
assert replies[11]['Ok'] is True and replies[12]['Ok'] is False
for i in [13,14,15,16]:assert replies[i]['Ok'] is False
assert replies[17]['Ok'] is True and not replies[18]['Ok'] and not replies[19]['Ok']
# The embedded script must boot without src/ or engine/. Native APIs are absent
# on Linux; their failures should remain structured partial-state errors.
exe=str(root/'GlassBoard.exe').replace("'","''")
bootstrap="$a=[Reflection.Assembly]::LoadFile('"+exe+"');$r=New-Object IO.StreamReader($a.GetManifestResourceStream('GlassBoard.SystemControl.ps1'));try{$s=$r.ReadToEnd()}finally{$r.Dispose()};& ([ScriptBlock]::Create($s))"
actual=run(bootstrap,'{"Version":1,"Id":1,"Action":"state"}\n{"Version":1,"Id":2,"Action":"lyrics"}\n')
assert actual[0]['Ok'] is True and isinstance(actual[0]['Result']['System']['Errors'],list)
assert actual[1]['Ok'] is False and 'Unknown control action' in actual[1]['Error']
print('PASS: shipped system script routing, Unicode/array JSON, native state/actions, invalid request isolation, rejected media/lyrics/transport, password non-disclosure and final EXE resource bootstrap. Windows hardware/PowerShell 5.1 remain untested.')
