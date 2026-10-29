from pathlib import Path
import subprocess,json,base64,tempfile,shutil,os,sys
root=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='音乐 module quote ') as folder:
 target=Path(folder)/"测试's folder";target.mkdir();exe=target/'GlassBoard.exe';shutil.copy2(root/'GlassBoard.exe',exe)
 env=dict(os.environ,GLASSBOARD_HOME=str(target))
 for name in ['MediaBridge.ps1','NativeMediaBridge.ps1']:
  resource='GlassBoard.'+name
  command="$a=[Reflection.Assembly]::LoadFile('"+str(exe).replace("'","''")+"');$r=New-Object IO.StreamReader($a.GetManifestResourceStream('"+resource+"'));try{$s=$r.ReadToEnd()}finally{$r.Dispose()};& ([ScriptBlock]::Create($s))"
  encoded=base64.b64encode(command.encode('utf-16le')).decode()
  run=subprocess.run([sys.argv[1],'-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',encoded],input='{"Command":"refresh"}\n',env=env,capture_output=True,text=True,timeout=25)
  lines=[json.loads(x) for x in run.stdout.splitlines() if x.startswith('{')]
  assert run.returncode==0 and len(lines)==1,(name,run.stdout,run.stderr)
  # Native Windows APIs are unavailable here; reaching a structured error proves the embedded module executed.
  assert not lines[0]['Available'] and 'Status' in lines[0],lines
  assert '媒体桥脚本缺失' not in lines[0]['Status'],lines
  print('PASS:',name,'actual EXE resource bootstrap, no external src folder, UTF-8 pipes, Unicode/space/apostrophe path; native API error expected on Linux')
