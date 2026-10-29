using System;using System.IO;using System.Runtime.Serialization.Json;
namespace GlassBoard {
    public static class NoteFiles {
        public static NoteBook Read(string path,Action<Exception> log=null){
            foreach(string file in new string[]{path,path+".bak"})try{if(File.Exists(file))using(var f=File.OpenRead(file)){var book=(NoteBook)new DataContractJsonSerializer(typeof(NoteBook)).ReadObject(f);if(book==null)throw new InvalidDataException("备忘录文件为空");book.Normalize();return book;}}catch(Exception ex){if(log!=null)log(ex);try{File.Copy(file,file+".damaged-"+DateTime.Now.ToString("yyyyMMddHHmmssfff"),false);}catch{}}
            var fresh=new NoteBook();fresh.Normalize();return fresh;
        }
        public static void Write(string path,NoteBook book){book.Normalize();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));string temp=path+".tmp";using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(NoteBook)).WriteObject(f,book);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    }
}
