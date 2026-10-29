"""Single unpack-and-run bundle with source, no separate source delivery."""
from pathlib import Path
import sys,json,hashlib,zipfile,io
root=Path(__file__).resolve().parents[1]
out=Path(sys.argv[1]).resolve();out.mkdir(parents=True,exist_ok=True)
version='0.2.0'
runtime=['GlassBoard.exe','GlassBoard.exe.config','Start.cmd','RestoreDesktop.cmd','RecoverWidgets.cmd','CheckWindows.cmd','CheckMemory.cmd','CollectDiagnostics.cmd','src/CheckMemory.ps1','src/CheckWindows.ps1','src/CollectDiagnostics.ps1','src/MediaBridge.ps1','src/NativeMediaBridge.ps1','lib/NPSMLib.dll','data/AppGroups.tsv','data/分组对照说明.md','licenses/NPSMLib_LICENSE.txt']
docs=['README.md','使用说明.md','CHANGELOG.md','RELEASE_NOTES_v0.9.23.md','LICENSE','THIRD_PARTY_NOTICES.md','Build.cmd','Build.ps1']
files=set(runtime+docs)
for folder in ['src','native','vendor','assets','verification']:
 for p in (root/folder).rglob('*'):
  if not p.is_file() or '__pycache__' in p.parts or p.suffix=='.pyc':continue
  rel=p.relative_to(root).as_posix()
  if rel=='assets/support/douyin.jpg':continue
  files.add(rel)
for f in files:assert (root/f).is_file(),f
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
raw=sum((root/f).stat().st_size for f in runtime)
buffer=io.BytesIO()
with zipfile.ZipFile(buffer,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for f in runtime:z.write(root/f,'GlassBoard/'+f)
report={'version':version,'build_revision':4,'single_bundle_with_source':True,'runtime_file_count':len(runtime),'runtime_unpacked_bytes':raw,'runtime_only_zip_estimate_bytes':len(buffer.getvalue()),'native_module_bytes':0,'rollback_from':'0.9.24','base_version':'0.9.23','system_glass_removed':True,'tray_mode':'Windows native popup','source_and_development_bytes':sum((root/f).stat().st_size for f in files if f not in runtime),'native_windows_execution_tested':False,'runtime_ram_measured':False,'assets_signature_private_key_included':False,'files':[{'path':f,'size':(root/f).stat().st_size,'sha256':sha(root/f)} for f in sorted(files)]}
(root/'package-size.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');files.add('package-size.json')
archive=out/'GlassBoard_SenlinSama_0.2.0_open_source.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for f in sorted(files):z.write(root/f,'GlassBoard/'+f)
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 assert 'GlassBoard/GlassBoard.exe' in z.namelist()
 assert not any('SystemGlass' in n or 'TrayPanelInteraction' in n or 'TrayPixelRules' in n for n in z.namelist())
 assert not any('engine/' in n or 'toolchain/' in n or n.endswith('.pem') or 'private-key' in n.lower() for n in z.namelist())
print(json.dumps({'archive':str(archive),'archive_bytes':archive.stat().st_size,'runtime_unpacked_bytes':raw,'runtime_only_zip_estimate_bytes':len(buffer.getvalue()),'native_module_bytes':report['native_module_bytes'],'source_and_development_bytes':report['source_and_development_bytes'],'sha256':sha(archive)},ensure_ascii=False,indent=2))
