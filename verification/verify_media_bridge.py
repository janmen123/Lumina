"""Run production PowerShell request loop with fake media sessions, without Windows APIs.
Usage: python verify_media_bridge.py /path/to/pwsh [--chinese]
This verifies routing/JSON/delayed-artwork logic, not native Windows behavior.
"""
from pathlib import Path
import subprocess,sys,json,tempfile
root=Path(__file__).resolve().parents[1]
source=(root/'src/MediaBridge.ps1').read_text(encoding='utf-8-sig')
pipes=source[source.index('$pipeInput='):source.index('$ProgressPreference')]
find=source[source.index('function Close-Safely'):source.index('function Initialize-MediaManager')]
loop=source[source.index('while ($null -ne ($line'):]
mock=r'''
$ErrorActionPreference='Stop'
Add-Type -Path '__DLL__'
Add-Type -TypeDefinition @'
using System;using System.IO;
public class ThrowDisposeStream : MemoryStream { public ThrowDisposeStream(byte[] bytes):base(bytes){} protected override void Dispose(bool disposing){base.Dispose(disposing);throw new Exception("mock dispose failure");} }
public class FakeProperties { public string Title="Track A",Artist="Artist A",AlbumTitle="Album"; public object Thumbnail=null; }
public class FakeControls { public bool IsPlaybackPositionEnabled=false,IsPreviousEnabled=true,IsNextEnabled=true,IsPlayEnabled=true,IsPauseEnabled=true; }
public class FakePlayback { public string PlaybackStatus="Playing"; public double PlaybackRate=1; public FakeControls Controls=new FakeControls(); }
public class FakeTimeline { public TimeSpan StartTime=TimeSpan.Zero,EndTime=TimeSpan.Zero,Position=TimeSpan.FromSeconds(20),MinSeekTime=TimeSpan.Zero,MaxSeekTime=TimeSpan.Zero; public DateTimeOffset LastUpdatedTime=DateTimeOffset.UtcNow; public DateTime PositionSetFileTime=DateTime.UtcNow; }
public class FakeNativePlayback { public int PlaybackCaps=32768; }
public class FakeNative { int covers; public bool Mismatch; public string GetParentApplicationId(){return Mismatch?"Other":"FakeMusic";} public FakeProperties GetMediaObjectInfo(){return new FakeProperties{Title=Mismatch?"Other Track":"Track A"};} public FakeNativePlayback GetMediaPlaybackInfo(){return new FakeNativePlayback();} public FakeTimeline GetMediaTimelineProperties(){return new FakeTimeline{EndTime=TimeSpan.FromSeconds(180),MaxSeekTime=TimeSpan.FromSeconds(180)};} public bool SendPlaybackPositionChangeRequest(TimeSpan value){return value.TotalSeconds==50;} public Stream GetThumbnailStream(){covers++;return covers==1?null:new ThrowDisposeStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jP1kAAAAASUVORK5CYII="));} }
public class FakeCandidate { public uint PID=0; public FakeNative Data=new FakeNative(); public FakeNative ActivateMediaPlaybackDataSource(){return Data;} }
public class FakeNpsm { public FakeCandidate Candidate=new FakeCandidate(); public FakeCandidate[] GetSessions(){return new[]{Candidate};} }
public class FakeSession { public string SourceAppUserModelId="FakeMusic"; public FakePlayback GetPlaybackInfo(){return new FakePlayback();} public FakeTimeline GetTimelineProperties(){return new FakeTimeline();} public FakeProperties TryGetMediaPropertiesAsync(){return new FakeProperties();} public bool TryChangePlaybackPositionAsync(long ticks){return false;} public bool TryTogglePlayPauseAsync(){return true;} public bool TrySkipPreviousAsync(){return true;} public bool TrySkipNextAsync(){return true;} }
public class FakeManager { public FakeSession GetCurrentSession(){return new FakeSession();} public FakeSession[] GetSessions(){return new[]{new FakeSession()};} }
'@
$manager=New-Object FakeManager
$npsm=New-Object FakeNpsm
$nativeAttempted=$true
__MISMATCH__
$lastInitialize=[DateTime]::UtcNow;$startupError=$null; $lastCoverKey=''; $cover=''; $lastCoverAttempt=[DateTime]::MinValue; $coverStatus='Not supplied'; $propertiesType=[object]
function Await($operation,$resultType){return $operation}
'''
track,artist,album=("再见","张震岳","思念是一种病") if len(sys.argv)>2 and sys.argv[2]=='--chinese' else ("Track A","Artist A","Album")
mock=mock.replace('Track A',track).replace('Artist A',artist).replace('AlbumTitle="Album"','AlbumTitle="'+album+'"')
key='FakeMusic|'+track+'|'+artist+'|'+album
requests=[{'Command':'cover'},{'Command':'cover'},{'Command':'seek','Seconds':50,'Key':key},{'Command':'seek','Seconds':90,'Key':'old track'}]
with tempfile.TemporaryDirectory() as folder:
 for mode in ('native','mismatch','standard','timeline-error','artwork-only'):
  mismatch=mode=='mismatch'
  p=Path(folder)/'bridge-mock.ps1'
  p.write_text(pipes+mock.replace('__DLL__',str(root/'lib/NPSMLib.dll').replace("'","''")).replace('__MISMATCH__','$npsm.Candidate.Data.Mismatch=$true' if mismatch else '')+find+loop)
  test_requests=[dict(r,UseNative=mode in ('native','mismatch'),CoverOnly=mode=='artwork-only') for r in requests]
  if mode in ('timeline-error','artwork-only'):
   p.write_text(p.read_text().replace('return new FakeTimeline();','throw new Exception("mock timeline failure");'))
  result=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-File',str(p)],input='\n'.join(json.dumps(r,ensure_ascii=False) for r in test_requests)+'\n',text=True,capture_output=True,check=True)
  rows=[json.loads(line) for line in result.stdout.splitlines()]
  assert len(rows)==4,(result.stdout,result.stderr)
  assert all(r.get('Available') for r in rows),rows
  if mode in ('mismatch','standard','timeline-error','artwork-only'):
   assert rows[0]['Duration']==0 and not rows[0]['CanSeek'] and not rows[1]['Cover'],rows
   assert rows[0]['Title']==track,rows
   if mode=='timeline-error':assert 'mock timeline failure' in rows[0]['TimelineStatus'],rows
   if mode=='artwork-only':assert rows[0]['TimelineStatus']=='OK',rows
  else:
   assert rows[0]['Backend']=='NPSMLib' and rows[0]['Duration']==180 and rows[0]['CanSeek'],rows
   assert not rows[0]['Cover'] and rows[1]['Cover'] and rows[1]['CoverStatus']=='NPSMLib thumbnail',rows
   assert rows[2]['Status']=='OK' and rows[2]['Position']==50,rows
   assert rows[3]['Status']!='OK',rows
print('PASS: production PowerShell loop, matching native timeline/seek fallback, delayed artwork retry, stale-track rejection, unrelated-session exclusion, standard native-off mode, cleanup/timeline error isolation.')

# The isolated worker uses the same production request loop and matching code.
native_source=(root/'src/NativeMediaBridge.ps1').read_text(encoding='utf-8-sig')
native_functions=native_source[native_source.index('function Close-Safely'):native_source.index('while($null')]
native_pipes=native_source[native_source.index('$pipeInput='):native_source.index('$npsm=$null')]
native_loop=native_source[native_source.index('while($null'):]
with tempfile.TemporaryDirectory() as folder:
 for mode in ("matched","mismatch","parent-error"):
  mismatch=mode=="mismatch"
  p=Path(folder)/'native-worker-mock.ps1'
  prefix=mock.replace('__DLL__',str(root/'lib/NPSMLib.dll').replace("'","''")).replace('__MISMATCH__','$npsm.Candidate.Data.Mismatch=$true' if mismatch else '')
  if mode=="parent-error":prefix=prefix.replace('return Mismatch?"Other":"FakeMusic";','throw new Exception("mock parent id error");')
  p.write_text(native_pipes+prefix+native_functions+native_loop)
  hint={'Source':'FakeMusic','Title':track,'Artist':artist,'Album':album,'Key':key}
  native_requests=[dict(hint,Command='cover'),dict(hint,Command='cover'),dict(hint,Command='seek',Seconds=50),dict(hint,Command='seek',Seconds=90,Key='stale')]
  result=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-File',str(p)],input='\n'.join(json.dumps(r,ensure_ascii=False) for r in native_requests)+'\n',text=True,capture_output=True,check=True)
  rows=[json.loads(line) for line in result.stdout.splitlines()]
  assert len(rows)==4,(result.stdout,result.stderr)
  if mismatch:assert not any(r['Available'] for r in rows),rows
  else:
   assert rows[0]['Available'] and rows[0]['Duration']==180 and rows[0]['CanSeek'],rows
   assert rows[1]['Cover'] and rows[1]['CoverStatus']=='NPSMLib thumbnail',rows
   assert rows[2]['Status']=='OK' and rows[2]['Position']==50,rows
   assert not rows[3]['Available'] and rows[3]['Status']=='Session identity mismatch',rows
print('PASS: isolated native worker, delayed artwork, cleanup exceptions, native seek, track/source identity checks.')

# Cover-only native requests must still return artwork when the timeline API fails.
with tempfile.TemporaryDirectory() as folder:
 p=Path(folder)/'native-cover-only.ps1'
 prefix=mock.replace('__DLL__',str(root/'lib/NPSMLib.dll')).replace('__MISMATCH__','').replace('return new FakeTimeline{EndTime=TimeSpan.FromSeconds(180),MaxSeekTime=TimeSpan.FromSeconds(180)};','throw new Exception("timeline must not be called");')
 p.write_text(native_pipes+prefix+native_functions+native_loop)
 hint={'Command':'cover','CoverOnly':True,'Source':'FakeMusic','Title':track,'Artist':artist,'Album':album,'Key':key}
 result=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-File',str(p)],input='\n'.join(json.dumps(hint,ensure_ascii=False) for _ in range(2))+'\n',text=True,capture_output=True,check=True)
 rows=[json.loads(line) for line in result.stdout.splitlines()]
 assert all(r['Available'] and r['Duration']==0 and not r['CanSeek'] for r in rows),rows
 assert rows[1]['Cover'] and rows[1]['CoverStatus']=='NPSMLib thumbnail',rows
print('PASS: cover-only native/standard modes skip timeline APIs while retaining delayed artwork.')

# Transport commands in cover-only mode must not read timeline before or after action.
with tempfile.TemporaryDirectory() as folder:
 p=Path(folder)/'transport-cover-only.ps1'
 prefix=mock.replace('__DLL__',str(root/'lib/NPSMLib.dll')).replace('__MISMATCH__','').replace('return new FakeTimeline();','throw new Exception("timeline must not be called after transport");')
 p.write_text(pipes+prefix+find+loop)
 requests=[{'Command':cmd,'CoverOnly':True,'UseNative':False} for cmd in ('previous','toggle','next')]
 result=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-File',str(p)],input='\n'.join(json.dumps(request) for request in requests)+'\n',text=True,capture_output=True,check=True)
 rows=[json.loads(line) for line in result.stdout.splitlines()]
 assert all(row['Available'] and row['Status']=='OK' and row['TimelineStatus']=='OK' and row['Duration']==0 for row in rows),rows
print('PASS: previous/toggle/next skip timeline before and after transport action.')
