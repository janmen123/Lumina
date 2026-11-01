using System;using System.IO;using System.IO.Compression;using System.Text;
namespace Lumina {
    // Real, editable files. CreateNew prevents overwriting an existing document.
    public static class BoardFiles {
        public static readonly string[] Extensions={".txt",".md",".docx",".xlsx"};
        static void Part(ZipArchive zip,string path,string xml){using(var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false)))writer.Write(xml);}
        public static void Create(string path){
            string ext=Path.GetExtension(path).ToLowerInvariant();
            if(Array.IndexOf(Extensions,ext)<0)throw new ArgumentException("请选择文本、Markdown、Word 或 Excel 文件格式。");
            bool created=false;
            try{using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){
                created=true;
                if(ext==".docx"||ext==".xlsx")using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){
                    const string types="http://schemas.openxmlformats.org/package/2006/content-types",rels="http://schemas.openxmlformats.org/package/2006/relationships",office="http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
                    string main=ext==".docx"?"word/document.xml":"xl/workbook.xml";
                    Part(zip,"[Content_Types].xml","<Types xmlns=\""+types+"\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/"+main+"\" ContentType=\"application/vnd.openxmlformats-officedocument."+(ext==".docx"?"wordprocessingml.document":"spreadsheetml.sheet")+".main+xml\"/>"+(ext==".xlsx"?"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>":"")+"</Types>");
                    Part(zip,"_rels/.rels","<Relationships xmlns=\""+rels+"\"><Relationship Id=\"rId1\" Type=\""+office+"officeDocument\" Target=\""+main+"\"/></Relationships>");
                    if(ext==".docx")Part(zip,main,"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p/><w:sectPr/></w:body></w:document>");
                    else{
                        Part(zip,main,"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"工作表1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                        Part(zip,"xl/_rels/workbook.xml.rels","<Relationships xmlns=\""+rels+"\"><Relationship Id=\"rId1\" Type=\""+office+"worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                        Part(zip,"xl/worksheets/sheet1.xml","<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData/></worksheet>");
                    }
                }
                stream.Flush(true);
            }}catch{if(created)try{File.Delete(path);}catch{}throw;}
        }
        public static string CreateFolder(string parent,string name){
            name=(name??"").Trim();
            if(name.Length==0||name=="."||name==".."||name.IndexOfAny(new[]{'\\','/',':','*','?','"','<','>','|'})>=0||name.EndsWith(".")||Array.Exists(name.ToCharArray(),c=>c<32)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new ArgumentException("请输入有效的文件夹名称。");
            string stem=name.Split('.')[0].ToUpperInvariant();
            if(stem=="CON"||stem=="PRN"||stem=="AUX"||stem=="NUL"||(stem.Length==4&&(stem.StartsWith("COM")||stem.StartsWith("LPT"))&&stem[3]>='1'&&stem[3]<='9'))throw new ArgumentException("此名称由 Windows 保留，请换一个名称。");
            string path=Path.Combine(parent,name);
            if(File.Exists(path)||Directory.Exists(path))throw new IOException("同名文件或文件夹已经存在，请换一个名称。");
            Directory.CreateDirectory(path);return path;
        }
    }
}
