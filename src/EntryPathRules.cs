using System;using System.IO;
namespace GlassBoard {
    public static class EntryPathRules {
        public static string Normalize(string path){
            if(string.IsNullOrWhiteSpace(path)||!Directory.Exists(path))return path;
            string full=System.IO.Path.GetFullPath(path),root=System.IO.Path.GetPathRoot(full);
            return string.Equals(full,root,StringComparison.OrdinalIgnoreCase)?root:full.TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar);
        }
        public static string Name(string path){
            if(!Directory.Exists(path))return DocumentRules.Supported(path)?System.IO.Path.GetFileName(path):System.IO.Path.GetFileNameWithoutExtension(path);
            var folder=new DirectoryInfo(Normalize(path));return string.IsNullOrEmpty(folder.Name)?folder.FullName:folder.Name;
        }
    }
}
