using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumina {
    internal static class DiagnosticText {
        static readonly Regex Fields = new Regex(@"(?im)\b(?:fgtitle|title|entry|path|target|extras|source)\s*=.*$", RegexOptions.Compiled);
        static readonly Regex Url = new Regex(@"(?i)\bhttps?://[^\s<>""']+", RegexOptions.Compiled);
        static readonly Regex Paths = new Regex(@"(?i)(?:\b[A-Z]:[\\/]|\\\\)[^\r\n\t""<>|]*", RegexOptions.Compiled);
        static readonly Regex Email = new Regex(@"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
        static readonly Regex Secret = new Regex(@"(?im)\b(?:password|passwd|token|api[_-]?key|authorization)\s*[:=].*$", RegexOptions.Compiled);

        internal static string Redact(string value) {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            string text = Fields.Replace(value, "[detail omitted]");
            text = Secret.Replace(text, "[credential omitted]");
            text = Url.Replace(text, "[url]");
            text = Paths.Replace(text, "[path]");
            return Email.Replace(text, "[email]");
        }
    }

    internal static class DiagnosticExport {
        internal static void Create(string destination) {
            // Explicit local export only. Never include layouts, notes, window inventories,
            // archives, or arbitrary files from the data directory.
            string output = Path.GetFullPath(destination);
            using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create)) {
                Write(archive, "README.txt", AppInfo.Display + "\r\n"
                    + "Local diagnostic export; common paths, URLs, email and labelled sensitive fields are redacted.\r\n"
                    + "Layouts and notes are excluded. Review free-form error text before sharing.\r\n"
                    + "OS: " + Environment.OSVersion + "; process: " + (Environment.Is64BitProcess ? "64" : "32") + " bit\r\n");
                foreach (string name in new[] { "error.log", "session.log" }) {
                    string path = Path.Combine(RuntimePaths.DataFolder, name);
                    if (!File.Exists(path)) continue;
                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                        // Bound export size, even for old unrotated logs.
                        bool tail = input.Length > 1024 * 1024;
                        if (tail) input.Seek(-1024 * 1024, SeekOrigin.End);
                        using (var reader = new StreamReader(input, Encoding.UTF8)) {
                            if (tail) reader.ReadLine();
                            Write(archive, name, DiagnosticText.Redact(reader.ReadToEnd()));
                        }
                    }
                }
            }
        }

        static void Write(ZipArchive archive, string name, string text) {
            using (var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(text);
        }
    }
}
