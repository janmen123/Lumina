"""Compile portable tests against current production C# models.
Usage: python3 verification/RunPortableTests.py /path/to/toolchain
Toolchain contains dotnet/dotnet, compiler/tasks/netcore/bincore/csc.dll,
and .NET 8 runtime in dotnet/shared/Microsoft.NETCore.App/<version>.
Windows/WPF/native UI acceptance remains separate (CheckWindows.cmd).
"""
from pathlib import Path
import sys, tempfile, subprocess, os, json
project=Path(__file__).resolve().parents[1]
toolchain=Path(sys.argv[1]).resolve()
runtime=next((toolchain/'dotnet/shared/Microsoft.NETCore.App').glob('8.*'))
env=dict(os.environ,DOTNET_ROLL_FORWARD='Major')
header='using System;using System.Linq;using System.IO;using System.Threading;using System.Text;using System.Collections.Generic;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;\nnamespace GlassBoard{\n'
with tempfile.TemporaryDirectory(prefix='glassboard097-tests-') as directory:
    work=Path(directory)
    main=(project/'src/GlassBoard.cs').read_text(encoding='utf-8-sig')
    (work/'ProductionPreferences.cs').write_text(header+main[main.index('    public static class LayoutRules'):main.index('    public static partial class Store')]+ '\n}')
    store=main[main.index('    public static partial class Store'):main.index('    public static class Native')]
    folder='System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassBoard")'
    assert folder in store
    store=store.replace(folder,'System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GlassBoard-store-test-"+Guid.NewGuid().ToString("N"))')
    (work/'ProductionStore.cs').write_text(header+store+'\n}')
    media=(project/'src/MediaServices.cs').read_text()
    (work/'MediaSnapshot.cs').write_text(header+media[media.index('    [DataContract] public class MediaSnapshot'):media.index('    public static class MediaFallback')]+ '\n}')
    weather=(project/'src/Widgets.cs').read_text()
    (work/'WeatherModels.cs').write_text('using System;using System.Linq;using System.IO;using System.Text;using System.Net;using System.Globalization;using System.Collections.Generic;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;\n'+weather[weather.index('namespace GlassBoard {'):weather.index('    public static class SystemMedia')]+'\n}')
    policy=(project/'src/DesktopWindowPolicy.cs').read_text()
    method=policy[policy.index('        public static int ToolStyle'):policy.index('        public static void Apply')]
    (work/'ShellStyle.cs').write_text('namespace GlassBoard { public static class ShellStylePolicy {\n'+method+'}}')
    controls=(project/'src/ControlCenter.cs').read_text()
    (work/'ControlOptions.cs').write_text(header+controls[controls.index('    [DataContract]public sealed class ControlCenterOptions'):controls.index('    public partial class Controller')]+ '\n}')
    (work/'MusicOptions.cs').write_text((project/'src/LegacyMusicOptions.cs').read_text())
    timing=(project/'src/StartupTiming.cs').read_text()
    (work/'StartupTimingRules.cs').write_text(timing[:timing.index('    public static class StartupService')]+ '\n}')
    base=[str(toolchain/'dotnet/dotnet'),str(toolchain/'compiler/tasks/netcore/bincore/csc.dll'),'/nologo','/nostdlib+','/langversion:5','/optimize+','/nowarn:SYSLIB0014']+['/reference:'+str(p) for p in runtime.glob('*.dll')]
    models=[str(work/n) for n in ['ProductionPreferences.cs','ProductionStore.cs','MediaSnapshot.cs','ControlOptions.cs','MusicOptions.cs','StartupTimingRules.cs']]+[str(project/'src'/n) for n in ['DockLayoutRules.cs','PersonalizationCore.cs','FeatureCore.cs','ProductCore.cs','DockBehaviorRules.cs','DockTaskRules.cs','EntryPathRules.cs','MaterialOptions.cs','UserFeedbackCore.cs','NoteFiles.cs','Persistence.cs','NewBoardFiles.cs','NativeWireless.cs','NativeAudio.cs','NativeBluetooth.cs','SystemBridge.cs','BoundedProcess.cs','MotionPresets.cs','WindowsHost.cs','InteractionRules.cs','ComponentFiles.cs','AiSupport.cs','CreatorIdentityProof.cs','CreatorAssets.cs']]
    tests=['MotionPresetsTests','BoundedProcessTests','RollbackTests','WeatherTransportTests','Feedback18Tests','NativeControlTests','Feedback16Tests','StartupTimingTests','ControlCenterTests','MixedDockTests','PersonalizationTests','DockTaskTests','PersistenceTests','DockPointerTests','UserFeedbackTests','DockBehaviorTests','AppearanceOptionsTests','ExperienceTests','CompatibilityTests']
    only=next((x.split("=",1)[1] for x in sys.argv if x.startswith("--only=")),None)
    if only: tests=[name for name in tests if name in only.split(",")]
    for name in tests:
        sources=([str(project/'src'/n) for n in ['UpdateApply.cs','UpdateService.cs']] if name.startswith('Update') else models+([str(work/'WeatherModels.cs'),str(work/'ShellStyle.cs'),str(project/'src/WeatherPresentation.cs'),str(project/'src/WeatherTransport.cs')] if name in ['CompatibilityTests','PersonalizationTests','WeatherTransportTests'] else []))
        args=base+['/target:exe','/out:'+str(work/(name+'.dll'))]+sources+[str(project/'verification'/(name+'.cs'))]
        compiled=subprocess.run(args,env=env,text=True,capture_output=True)
        if compiled.returncode:print(compiled.stdout+compiled.stderr);raise SystemExit(compiled.returncode)
        (work/(name+'.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
        args=[str(toolchain/'dotnet/dotnet'),str(work/(name+'.dll'))]
        if name=='RollbackTests':args.append(str(project))
        if name=='BoundedProcessTests':args.append(str(toolchain/'dotnet/dotnet'))
        if name in ['UpdateTests','CompatibilityTests']:
            args.append(str(project/'GlassBoard.exe'))
            if '--live' in sys.argv:args.append('--live')
        ran=subprocess.run(args,env=env,text=True,capture_output=True);print(name+': '+ran.stdout.strip(),flush=True)
        if ran.returncode:print(ran.stderr);raise SystemExit(ran.returncode)
