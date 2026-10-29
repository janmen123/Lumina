from pathlib import Path
import subprocess,json,tempfile,sys
root=Path(__file__).resolve().parents[1]
s=(root/'src/NativeMediaBridge.ps1').read_text(encoding='utf-8-sig')
pipes=s[s.index('$pipeInput='):s.index('$npsm=$null')]
functions=s[s.index('function Close-Safely'):s.index('while($null')]
loop=s[s.index('while($null'):]
mock=r'''
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;using System.IO;
namespace NPSMLib { public enum MediaPlaybackCommands {Play=0,Pause=1,Next=6,Previous=7,PlayPauseToggle=10} }
public class Info {public string Title="再见",Artist="张震岳",AlbumTitle="思念是一种病";}
public class State {public string PlaybackState="Playing";public int PlaybackCaps=1219;}
public class Data {public string Source="cloudmusic";public Info Info=new Info();public State State=new State();public Info GetMediaObjectInfo(){return Info;}public string GetParentApplicationId(){return Source;}public State GetMediaPlaybackInfo(){return State;}public bool SendMediaPlaybackCommand(NPSMLib.MediaPlaybackCommands c){return true;}public Stream GetThumbnailStream(){return new MemoryStream(new byte[]{1,2,3});}}
public class Candidate {public int PID=1;public bool Broken;public Data Data=new Data();public Data ActivateMediaPlaybackDataSource(){if(Broken)throw new Exception("bad stale session");return Data;}}
public class Manager {public Candidate Broken=new Candidate{Broken=true};public Candidate Paused=new Candidate{Data=new Data{Source="Browser",Info=new Info{Title="网页"},State=new State{PlaybackState="Paused"}}};public Candidate Playing=new Candidate();public Candidate[] GetSessions(){return new[]{Broken,Paused,Playing};}}
'@
$npsm=New-Object Manager;$startupError=$null;$lastInitialize=[DateTime]::UtcNow
'''
requests=[{'Command':'discover'}, {'Command':'toggle','Source':'cloudmusic','Key':'native|cloudmusic|再见|张震岳|思念是一种病'}, {'Command':'next','Key':'native|cloudmusic|另一首|张震岳|思念是一种病'}, {'Command':'discover'}]
with tempfile.TemporaryDirectory() as d:
 p=Path(d)/'native.ps1';p.write_text(mock+pipes+functions+loop)
 run=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-File',str(p)],input=''.join(json.dumps(x,ensure_ascii=False)+'\n' for x in requests),capture_output=True,text=True,timeout=30)
 if run.returncode:raise RuntimeError(run.stderr)
 replies=[json.loads(x) for x in run.stdout.splitlines() if x.strip()]
 assert len(replies)==4,(run.stdout,run.stderr)
 assert replies[0]['Available'] and replies[0]['Source']=='cloudmusic' and replies[0]['Backend']=='NPSMLib direct',replies
 assert replies[0]['Title']=='再见' and replies[0]['Playing'] and replies[0]['Cover']=='AQID',replies
 assert replies[1]['Status']=='OK',replies
 assert not replies[2]['Available'],replies
 assert replies[3]['Available'],replies
 print('PASS: production native discovery skips broken/stale sessions, selects playing music, Unicode metadata/artwork, routes controls, rejects stale track identity and recovers on next read')
