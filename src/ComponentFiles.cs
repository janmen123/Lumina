using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Security.Cryptography;
namespace GlassBoard {
    public sealed class ComponentFile {public string Name,Path,Status;public bool Valid,Blocked;}
    public static class ComponentFiles {
        static readonly string[] Names={"lib/NPSMLib.dll"};
        static readonly string[] Hashes={"063614ea251ebebee8a65c16d0a2f80469163da58a9972d0136b60124701bdd5"};
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool DeleteFile(string path);
        public static List<ComponentFile> Inspect(string root){var result=new List<ComponentFile>();for(int i=0;i<Names.Length;i++){
            var f=new ComponentFile{Name=Names[i],Path=System.IO.Path.Combine(root,Names[i].Replace('/',System.IO.Path.DirectorySeparatorChar))};
            if(!File.Exists(f.Path))f.Status="缺失，请重新解压完整用户包";
            else {using(var stream=File.OpenRead(f.Path))using(var sha=SHA256.Create())f.Valid=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").Equals(Hashes[i],StringComparison.OrdinalIgnoreCase);
                try{using(var stream=File.OpenRead(f.Path+":Zone.Identifier")){f.Blocked=stream.Length>0;}}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
                f.Status=!f.Valid?"文件与本版不一致，请重新解压":f.Blocked?"校验通过 · 带有下载阻止标记":"校验通过 · 未发现下载阻止标记";
            }result.Add(f);
        }return result;}
        public static int Repair(IEnumerable<ComponentFile> files){int count=0;foreach(var f in files.Where(x=>x.Valid&&x.Blocked)){
            // Recheck bytes immediately before removing only this file's Zone.Identifier.
            string root=AppDomain.CurrentDomain.BaseDirectory;var current=Inspect(root).FirstOrDefault(x=>x.Name==f.Name&&x.Path==f.Path);if(current==null||!current.Valid||!current.Blocked)continue;
            if(!DeleteFile(current.Path+":Zone.Identifier"))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());count++;
        }return count;}
    }
}
