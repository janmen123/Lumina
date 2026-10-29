"""Cross-compile the Windows WPF host against official .NET Framework 4.8 refs.
Usage: python3 verification/BuildPortable.py /absolute/toolchain
Builds only the .NET Framework host; no runtime is shipped.
"""
from pathlib import Path
import subprocess,sys
root=Path(__file__).resolve().parents[1]
chain=Path(sys.argv[1]).resolve()
refs=chain/'net48/build/.NETFramework/v4.8'
base=[str(chain/'dotnet/dotnet'),str(chain/'compiler/tasks/netcore/bincore/csc.dll'),'/nologo','/nostdlib+','/langversion:5','/optimize+','/codepage:65001']
common=['mscorlib','System','System.Core','System.Runtime.Serialization','System.Xml','System.IO.Compression','System.IO.Compression.FileSystem']
def reference(name):
    p=refs/(name+'.dll')
    if not p.exists():p=refs/'WPF'/(name+'.dll')
    return '/reference:'+str(p)
app=base+['/target:winexe','/out:'+str(root/'GlassBoard.exe'),'/win32icon:'+str(root/'src/GlassBoard.ico'),'/win32manifest:'+str(root/'src/app.manifest')]+[reference(n) for n in common+['System.Xml.Linq','System.Management','System.Drawing','System.Windows.Forms','WindowsBase','PresentationCore','PresentationFramework','System.Xaml','UIAutomationClient','UIAutomationTypes']]
app.append('/link:'+str(root/'verification/build-refs/Interop.UIAutomationClient.dll'))
for file,name in [('assets/support/c1.dat','GlassBoard.Assets.c1.dat'),('assets/support/c2.dat','GlassBoard.Assets.c2.dat'),('assets/support/c3.dat','GlassBoard.Assets.c3.dat'),('src/MediaBridge.ps1','GlassBoard.MediaBridge.ps1'),('src/NativeMediaBridge.ps1','GlassBoard.NativeMediaBridge.ps1'),('src/SystemControl.ps1','GlassBoard.SystemControl.ps1')]:app.append('/resource:'+str(root/file)+','+name)
app += [str(p) for p in sorted((root/'src').glob('*.cs'))]
subprocess.run(app,check=True)
print('PASS: WPF host cross-compiled for .NET Framework 4.8; no updater or redistributable runtime. Windows execution remains required.')
