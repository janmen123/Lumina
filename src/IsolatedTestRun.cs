using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Lumina {
    internal static class IsolatedTestRun {
        internal static int Run(bool ui) {
            // Set before any Store member is touched: Store caches its paths on first use.
            string folder = Path.Combine(Path.GetTempPath(), "Lumina-regression-" + Guid.NewGuid().ToString("N"));
            string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-isolation-result.txt");
            AppDomain.CurrentDomain.SetData("Lumina.TestDataFolder", folder);
            int result = 1;
            string failure = null;
            try {
                if (RuntimePaths.DataFolder != folder) throw new InvalidOperationException("Test data isolation failed.");
                Directory.CreateDirectory(folder);
                result = Execute(ui, folder);
            } catch (Exception ex) {
                failure = "isolated test startup: " + ex;
                result = 1;
            } finally {
                // Do not restore the AppDomain value: Store's cached paths must remain isolated
                // until this one-shot process exits, including finalizers and late callbacks.
                try {
                    if (RuntimePaths.DataFolder != folder) throw new InvalidOperationException("Test folder changed.");
                    if (Directory.Exists(folder)) {
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("Refusing recursive cleanup of a redirected test directory.");
                        Directory.Delete(folder, true);
                    }
                } catch (Exception ex) {
                    result = 1;
                    failure = (failure ?? "") + "\r\ntest directory cleanup: " + ex;
                }
            }
            File.WriteAllText(report, (result == 0 ? "PASS" : "FAIL")
                + (failure == null ? ": isolated temporary data directory; production Store path verified; cleanup complete." : ": " + failure)
                + Environment.NewLine);
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Execute(bool ui, string expected) {
            if (Store.Folder != expected || Path.GetDirectoryName(Store.FileName) != expected)
                throw new InvalidOperationException("Store was initialized before test isolation.");
            Store.Silent = true;
            // Exercise the shared logging sink inside the test directory as well.
            Store.Trace("START isolated self-test");
            if (!File.Exists(Path.Combine(expected, "session.log")))
                throw new IOException("The diagnostic sink did not use the isolated directory.");
            return ui ? SelfTest.Run() : LogicTests.Run();
        }
    }
}
