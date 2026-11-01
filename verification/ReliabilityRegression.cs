using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Lumina;

public static class ReliabilityRegression {
    static int checks;
    static void Check(bool value, string label) {
        checks++;
        if (!value) throw new Exception(label);
    }
    static byte[] Json<T>(T value) {
        using (var stream = new MemoryStream()) {
            new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
            return stream.ToArray();
        }
    }

    [STAThread]
    public static int Main() {
        string folder = Path.Combine(Path.GetTempPath(), "Lumina-regression-" + Guid.NewGuid().ToString("N"));
        AppDomain.CurrentDomain.SetData("Lumina.TestDataFolder", folder);
        try {
            Check(Store.Folder == folder, "Tests must never use the real profile");
            Check(PersistenceTests.Main() == 0, "Existing production persistence suite");
            Check(DockPointerTests.Main() == 0, "Existing Dock drag/drop persistence suite");
            Check(DockBehaviorTests.Main() == 0, "Existing Dock behavior suite");
            Run();
            Console.WriteLine("PASS: reliability regression ({0} assertions plus existing suites)", checks);
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        } finally {
            string expected = Path.GetFullPath(folder);
            if (expected.StartsWith(Path.Combine(Path.GetTempPath(), "Lumina-regression-"), StringComparison.OrdinalIgnoreCase)
                && Path.GetDirectoryName(expected) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)
                && Directory.Exists(expected)) Directory.Delete(expected, true);
        }
    }

    static void Run() {
        var savedCulture=System.Threading.Thread.CurrentThread.CurrentCulture;
        try {
            System.Threading.Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var day=new DateTime(2026,11,2,9,5,0);
            Check(ClockCatalog.DateLabel(day,10)=="星期一 · 11月2日", "Magazine date is Chinese even on English Windows");
            Check(ClockCatalog.DateLabel(day,1)=="11月2日", "Layered clock date is Chinese");
            Check(ClockCatalog.DateLabel(day,5)=="2026年11月2日", "Spaced clock date is Chinese");
            Check(ClockCatalog.MonthLabel(day)=="2026年11月", "Poster month is Chinese");
            Check(ClockCatalog.PeriodLabel(day)=="上午"&&ClockCatalog.PeriodLabel(day.AddHours(6))=="下午", "AM and PM are Chinese");
            for(int style=0;style<ClockCatalog.Names.Length;style++) {
                Check(ClockCatalog.Weekday(day,style)=="星期一", "Chinese weekday for clock style "+style);
                Check(!System.Text.RegularExpressions.Regex.IsMatch(ClockCatalog.DateLabel(day,style),"[A-Za-z]"), "No English date for style "+style);
            }
        } finally { System.Threading.Thread.CurrentThread.CurrentCulture=savedCulture; }

        var pause = new ScanPauseState();
        pause.Set(ScanPauseReason.Fullscreen, true);
        pause.Set(ScanPauseReason.Game, false);
        Check(pause.Paused, "Disabling game mode must not resume fullscreen scanning");
        pause.Set(ScanPauseReason.Game, true);
        pause.Set(ScanPauseReason.Fullscreen, false);
        Check(pause.Paused, "Leaving fullscreen must not override manual game mode");
        pause.Set(ScanPauseReason.Game, false);
        Check(!pause.Paused, "Scanning resumes after both reasons clear");

        Check(WindowReliabilityRules.ContinueHold(true, false, 0.3, 3), "Valid hold");
        Check(!WindowReliabilityRules.ContinueHold(true, true, 0.3, 3), "User switching apps cancels hold");
        Check(!WindowReliabilityRules.ContinueHold(false, false, 0.3, 3), "Closed/reused window cancels hold");
        Check(!WindowReliabilityRules.ContinueHold(true, false, 3, 3), "Hold expires on elapsed time");
        Check(!WindowReliabilityRules.ContinueHold(true, false, 0, 0), "Disabled hold");
        Check(!WindowReliabilityRules.ContinueHold(true, false, 10, int.MaxValue), "Hold is bounded despite corrupt settings");
        Check(WindowReliabilityRules.Fullscreen(true, false, false, false, true), "Borderless fullscreen");
        Check(!WindowReliabilityRules.Fullscreen(true, false, true, true, true), "Maximized desktop window is not fullscreen");
        Check(!WindowReliabilityRules.Fullscreen(true, true, false, false, true), "Minimized window is not fullscreen");
        Check(!WindowReliabilityRules.Fullscreen(false, false, false, false, true), "Invisible window is not fullscreen");

        var candidates = new[] { @"C:\Apps\Client\client.exe" };
        Check(WindowActivationRules.FallbackMatch(candidates, null, "client.exe"), "Elevated name fallback");
        Check(WindowActivationRules.FallbackMatch(candidates, @"C:\Apps\Client\bin\client.exe", null), "Same installation bin helper");
        Check(!WindowActivationRules.FallbackMatch(candidates, @"D:\Other\client.exe", null), "Do not activate unrelated installation");
        Check(!WindowActivationRules.FallbackMatch(new[] { @"C:\Apps\launcher.exe" }, null, "launcher.exe"), "Generic launcher names are not identities");
        var launcherEntry = new Entry { Name = "Client", Path = candidates[0] };
        var wrong = new RunningWindow { Path = @"D:\Other\client.exe", Title = "Client" };
        Check(RunningWindows.MatchWindows(launcherEntry, candidates, new[] { wrong }).Count == 0, "Actual selection cannot override executable identity with title");
        var folderWindow = new RunningWindow { Class = "CabinetWClass", Path = @"C:\Windows\explorer.exe" };
        var explorer = new Entry { Path = @"C:\Windows\explorer.exe", LaunchTarget = @"C:\Windows\explorer.exe" };
        Check(RunningWindows.MatchWindows(explorer, WindowActivationRules.Executables(explorer), new[] { folderWindow }).Count == 1, "Explorer matches before folder cache refresh");
        Check(RunningWindows.MatchWindows(explorer, WindowActivationRules.Executables(explorer), new[] { new RunningWindow { Path = explorer.Path, Class = "WorkerW" } }).Count == 0, "Explorer desktop is not a file-manager window");

        var attempted = new List<int>();
        bool aggregated = false;
        try {
            RecoveryActions.RunAll(new Action[] {
                () => { attempted.Add(1); throw new IOException("icon failure"); },
                () => { attempted.Add(2); },
                () => { attempted.Add(3); throw new IOException("other failure"); }
            });
        } catch (AggregateException ex) { aggregated = ex.InnerExceptions.Count == 2; }
        Check(aggregated && attempted.SequenceEqual(new[] { 1, 2, 3 }), "One failed recovery must not prevent the others");

        Directory.CreateDirectory(Store.Folder);
        var prefs = new Preferences { Imported = true, FormatVersion = 2 };
        prefs.Apps.Add(new Entry { Name = "Private document title", Path = @"C:\Private\report.lnk" });
        Store.Save(prefs);
        byte[] good = File.ReadAllBytes(Store.FileName);
        // Valid JSON with invalid nested values must fall back just like invalid JSON.
        var invalid = new Preferences { FormatVersion = 2 };
        invalid.Apps.Add(new Entry { Name = null, Path = "\0" });
        File.WriteAllBytes(Store.FileName, Json(invalid));
        var recovered = Store.Load();
        Check(recovered.Apps.Count == 1 && recovered.Apps[0].Name == "Private document title", "Invalid nested configuration uses recovery copy");
        Check(Store.RecoveryMessage != null, "Recovery is reported");
        Store.Save(recovered);
        Check(Store.ReadLayout(File.ReadAllBytes(Store.FileName)).Apps.Count == 1, "Recovered layout persists");

        var future = new Preferences { FormatVersion = 99 };
        var futureBytes = Json(future);
        File.WriteAllBytes(Store.FileName, futureBytes);
        bool refused = false;
        try { Store.Load(); } catch (UnsupportedLayoutException) { refused = true; }
        Check(refused && File.ReadAllBytes(Store.FileName).SequenceEqual(futureBytes), "Newer config cannot be silently downgraded to backup");
        refused = false;
        try { Store.Save(future); } catch (UnsupportedLayoutException) { refused = true; }
        Check(refused && File.ReadAllBytes(Store.FileName).SequenceEqual(futureBytes), "Saving newer config is refused without writing");
        File.WriteAllBytes(Store.FileName, good);
        prefs = Store.Load();
        prefs.KeepWindowSeconds = int.MaxValue;
        prefs.Normalize();
        Check(prefs.KeepWindowSeconds == 10, "Hold setting upper bound");
        prefs.KeepWindowSeconds = -9;
        prefs.Normalize();
        Check(prefs.KeepWindowSeconds == 0, "Hold setting lower bound");

        string redacted = DiagnosticText.Redact("error C:\\Users\\Private User\\Secret.docx\r\nhttps://example.test/?token=secret\r\nprivate@example.test\r\nfgtitle=Private title\r\napi_key=secret");
        Check(!new[] { "Private User", "Secret.docx", "example.test", "Private title", "secret" }.Any(redacted.Contains), "Diagnostic redaction");
        string unc = DiagnosticText.Redact(@"failure \\private-server\team\report.txt");
        Check(!unc.Contains("private-server") && !unc.Contains("report.txt"), "UNC path redaction");
        Store.Diagnostics = false;
        string log = Path.Combine(Store.Folder, "session.log");
        if (File.Exists(log)) File.Delete(log);
        Store.Trace("WINDOW private activity");
        Check(!File.Exists(log), "Detailed trace is opt-in");
        Parallel.For(0, 40, i => Store.Trace("SAVE test=" + i));
        Check(File.ReadAllLines(log).Length == 40, "Concurrent logs are not dropped or interleaved");

        File.WriteAllText(Path.Combine(Store.Folder, "error.log"), "error C:\\Users\\Private User\\Secret.docx\r\nfgtitle=Private title");
        File.WriteAllText(Path.Combine(Store.Folder, "notes.json"), "confidential note");
        string zip = Path.Combine(Store.Folder, "export.zip");
        DiagnosticExport.Create(zip);
        using (var archive = ZipFile.OpenRead(zip)) {
            Check(archive.Entries.All(e => new[] { "README.txt", "error.log", "session.log" }.Contains(e.FullName)), "Export allowlist excludes layouts and notes");
            foreach (var entry in archive.Entries) using (var reader = new StreamReader(entry.Open())) {
                string text = reader.ReadToEnd();
                Check(!text.Contains("Private User") && !text.Contains("Secret.docx") && !text.Contains("Private title") && !text.Contains("confidential note"), "Export content is redacted: " + entry.FullName);
            }
        }
        bool collision = false;
        try { DiagnosticExport.Create(zip); } catch (IOException) { collision = true; }
        Check(collision, "Export never overwrites existing files");
    }
}
