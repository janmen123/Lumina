using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using Lumina;
class DeepAuditRegression {
 static int failed,passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static void Case(string name,Action run){try{run();passed++;Console.WriteLine("PASS: "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL: "+name+" - "+e.GetBaseException().Message);}}
 static void Write<T>(string path,T value){using(var s=File.Create(path))new DataContractJsonSerializer(typeof(T)).WriteObject(s,value);}
 static Preferences Prefs(){var p=new Preferences();p.Groups=new List<string>{"Work","Docs"};p.DesktopFolders=true;p.DesktopFolderGroup="Work";p.DesktopDocs=true;p.DesktopDocsGroup="Docs";p.Apps.Add(new Entry{Path="a.exe",Name="A",Group="Work"});p.Normalize();return p;}
 static int Main(){
 string root=Path.Combine(Path.GetTempPath(),"Lumina-regression-deep-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);AppDomain.CurrentDomain.SetData("Lumina.TestDataFolder",root);
 try{
 Case("backup without legacy notes exports and restores",()=>{var from=Path.Combine(root,"plain");Directory.CreateDirectory(from);Write(Path.Combine(from,"layout.json"),Prefs());var zip=Path.Combine(root,"plain.gbbackup");BackupFiles.Create(from,zip);var dest=Path.Combine(root,"plain-restored");BackupFiles.Restore(zip,dest);Check(Store.ReadLayout(File.ReadAllBytes(Path.Combine(dest,"layout.json"))).Apps.Count==1,"layout lost");});
 Case("backup preserves existing legacy notes byte-for-byte",()=>{var from=Path.Combine(root,"notes");Directory.CreateDirectory(from);Write(Path.Combine(from,"layout.json"),Prefs());Write(Path.Combine(from,"notes.json"),new NoteBook{Items=new List<NoteItem>{new NoteItem{Body="Keep this legacy text"}}});var zip=Path.Combine(root,"notes.gbbackup");BackupFiles.Create(from,zip);var dest=Path.Combine(root,"notes-restored");BackupFiles.Restore(zip,dest);Check(File.ReadAllBytes(Path.Combine(from,"notes.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(dest,"notes.json"))),"notes changed");});
 Case("renaming groups includes case-variant memberships and learned rules",()=>{var p=Prefs();p.Apps[0].Group="work";p.Apps[0].ExtraGroups=new List<string>{"WORK","Docs"};p.LearnedGroups["a"]="WORK";FeatureRules.RenameGroup(p,"Work","Projects");Check(p.Apps[0].Group=="Projects"&&!p.Groups.Any(g=>g.Equals("Work",StringComparison.OrdinalIgnoreCase))&&p.LearnedGroups["a"]=="Projects","old group survives rename");});
 Case("undo and redo rename restore automatic desktop destinations",()=>{var p=Prefs();var h=new BoardEditHistory();var before=BoardEditState.Capture(p);FeatureRules.RenameGroup(p,"Work","Projects");h.Remember(before,p,"rename");h.Undo(p);Check(p.DesktopFolderGroup=="Work","undo left renamed auto destination");h.Redo(p);Check(p.DesktopFolderGroup=="Projects","redo lost auto destination");});
 Case("undo delete restores automatic desktop collection switches",()=>{var p=Prefs();var h=new BoardEditHistory();var before=BoardEditState.Capture(p);FeatureRules.DeleteGroup(p,"Docs");h.Remember(before,p,"delete");Check(!p.DesktopDocs,"delete should disable auto docs");h.Undo(p);Check(p.DesktopDocs&&p.DesktopDocsGroup=="Docs","undo did not restore auto docs");h.Redo(p);Check(!p.DesktopDocs,"redo should disable auto docs");});
 Case("restore without notes preserves destination legacy notes",()=>{var dest=Path.Combine(root,"existing-notes");Directory.CreateDirectory(dest);var note=Path.Combine(dest,"notes.json");File.WriteAllText(note,"existing content");BackupFiles.Restore(Path.Combine(root,"plain.gbbackup"),dest);Check(File.ReadAllText(note)=="existing content","optional notes were erased");});
 Case("corrupt notes cannot replace an existing backup",()=>{var from=Path.Combine(root,"notes");File.WriteAllText(Path.Combine(from,"notes.json"),"broken");var zip=Path.Combine(root,"notes.gbbackup");var saved=File.ReadAllBytes(zip);bool rejected=false;try{BackupFiles.Create(from,zip);}catch{rejected=true;}Check(rejected&&File.ReadAllBytes(zip).SequenceEqual(saved),"existing backup damaged");});
 Case("failed restore rolls back files already committed",()=>{var dest=Path.Combine(root,"rollback");Directory.CreateDirectory(dest);var target=Path.Combine(dest,"layout.json");File.WriteAllText(target,"original layout");bool rejected=false;try{BackupFiles.Restore(Path.Combine(root,"plain.gbbackup"),dest,name=>{throw new IOException("simulated disk failure");});}catch(IOException){rejected=true;}Check(rejected&&File.ReadAllText(target)=="original layout","rollback lost original");});
 Case("unsupported future layout cannot become a restorable backup",()=>{var from=Path.Combine(root,"future");Directory.CreateDirectory(from);var p=Prefs();p.FormatVersion=999;Write(Path.Combine(from,"layout.json"),p);bool rejected=false;try{BackupFiles.Create(from,Path.Combine(root,"future.gbbackup"));}catch{rejected=true;}Check(rejected&&!File.Exists(Path.Combine(root,"future.gbbackup")),"future schema accepted");});
 Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;
 }finally{if(Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(),"Lumina-regression-deep-"),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
 }
}
