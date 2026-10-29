using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.Serialization.Json;using System.IO.Compression;
namespace GlassBoard {
    public static partial class Store {
        static readonly object saveLock=new object();
        static byte[] lastSaved;static string lastInventory;static DateTime lastSnapshot;static long latestRevision;
        public static string RecoveryMessage{get;private set;}
        static byte[] Encode<T>(T value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return stream.ToArray();}}
        public static Preferences ReadLayout(byte[] bytes){if(bytes==null||bytes.Length>8*1024*1024)throw new InvalidDataException("配置文件过大");using(var stream=new MemoryStream(bytes,false)){var p=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream);if(p==null||p.Apps==null)throw new InvalidDataException("配置缺少入口清单");return p;}}
        static void Atomic(string path,byte[] bytes,string backup=null){
            string temp=path+".write-"+Guid.NewGuid().ToString("N");
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,backup);else File.Move(temp,path);}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
        }
        static IEnumerable<string> RecoveryPaths(){
            var files=new List<string>{FileName+".last-good",FileName+".previous",FileName+".tmp"};
            string folder=Path.Combine(Folder,"snapshots");if(Directory.Exists(folder))files.AddRange(Directory.GetFiles(folder,"layout-*.json"));
            // Recover the newest readable live save, not an older .previous before
            // checking a newer snapshot. Upgrade/manual copies are a last resort.
            files=files.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            if(Directory.Exists(folder))files.AddRange(Directory.GetFiles(folder,"upgrade-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(20));files.AddRange(Directory.GetFiles(Folder,"layout.json.backup-*").Concat(Directory.GetFiles(Folder,"layout.json.before-*")).OrderByDescending(File.GetLastWriteTimeUtc).Take(30));return files;
        }
        static string ContentDigest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static void ArchiveUpgrade(byte[] bytes){try{string folder=Path.Combine(Folder,"snapshots");Directory.CreateDirectory(folder);string hash=ContentDigest(bytes);string path=Path.Combine(folder,"upgrade-"+hash+".json");if(!File.Exists(path))Atomic(path,bytes);}catch(Exception ex){Log(ex);}}
        public static Preferences Load(bool mergeJournal=true){lock(saveLock){
            Directory.CreateDirectory(Folder);RecoveryMessage=null;lastSaved=null;bool damaged=false;Preferences prefs=null;byte[] bytes=null;
            if(File.Exists(FileName))try{bytes=File.ReadAllBytes(FileName);prefs=ReadLayout(bytes);}catch(Exception ex){damaged=true;Log(ex);try{File.Copy(FileName,FileName+".backup-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),false);}catch{}}
            if(prefs==null){foreach(string candidate in RecoveryPaths())if(File.Exists(candidate))try{bytes=File.ReadAllBytes(candidate);prefs=ReadLayout(bytes);RecoveryMessage="已从安全副本恢复布局。可在设置 → 通用 → 找回旧版入口中检查历史条目。";break;}catch(Exception ex){damaged=true;Log(ex);}}
            if(prefs==null){if(damaged)throw new InvalidDataException("配置和安全副本均无法读取，原文件已保留。请使用完整备份恢复，避免覆盖原配置。配置目录："+Folder);prefs=new Preferences();}
            else if(prefs.FormatVersion<2)ArchiveUpgrade(bytes);
            latestRevision=Math.Max(latestRevision,prefs.SaveRevision);
            var journal=ReadJournal();if(journal!=null){latestRevision=Math.Max(latestRevision,journal.SaveRevision);if(RecoveryMessage!=null&&mergeJournal)MergeRecoveryJournal(prefs,journal);}
            prefs.Normalize();lastSaved=bytes;Trace("LOAD entries="+prefs.Apps.Count+" groups="+prefs.Groups.Count+" recovered="+(RecoveryMessage!=null));return prefs;
        }}
        static RemovalJournal ReadJournal(){try{string path=Path.Combine(Folder,"removed.json");if(!File.Exists(path))return null;using(var stream=File.OpenRead(path))return (RemovalJournal)new DataContractJsonSerializer(typeof(RemovalJournal)).ReadObject(stream);}catch(Exception ex){Log(ex);return null;}}
        static void MergeRecoveryJournal(Preferences prefs,RemovalJournal journal){
            // The matching/newer layout already carries its own removals. An old
            // journal left behind by a failed write must not erase a manual re-add.
            if(prefs.SaveRevision>0&&journal.SaveRevision<=prefs.SaveRevision)return;
            journal.Merge(prefs);
            // New journals also record live paths. A manually restored alias remains live
            // even when an older snapshot and an unrelated tombstone share its executable.
            prefs.Apps.RemoveAll(a=>!(journal.ActivePaths??new List<string>()).Contains(a.Path,StringComparer.OrdinalIgnoreCase)&&RemovalRules.Blocked(prefs,a));
        }
        public static void Save(Preferences p){lock(saveLock){
            Directory.CreateDirectory(Folder);p.FormatVersion=2;RemovalRules.ReconcileActive(p);
            byte[] bytes=Encode(p);ReadLayout(bytes);
            // A previous attempt may have committed the main file but failed to
            // write its mirror. Also repair a deleted/truncated main file even
            // when the in-memory layout has not changed since the last save.
            bool changed=lastSaved==null||!bytes.SequenceEqual(lastSaved);bool unchanged=!changed&&Matches(FileName,bytes);
            // The authoritative layout commits first. A stale deletion journal must never
            // override a successful save or prevent a deliberate manual re-add.
            string previous=null;if(File.Exists(FileName))try{ReadLayout(File.ReadAllBytes(FileName));previous=FileName+".previous";}catch{}
            if(!unchanged){if(changed){p.SaveRevision=checked(Math.Max(latestRevision,p.SaveRevision)+1);bytes=Encode(p);}Atomic(FileName,bytes,previous);latestRevision=p.SaveRevision;lastSaved=bytes;Trace("SAVE entries="+p.Apps.Count+" groups="+p.Groups.Count+" revision="+p.SaveRevision);}
            try{if(!Matches(FileName+".last-good",bytes))Atomic(FileName+".last-good",bytes);}catch(Exception ex){Log(ex);}
            try{var journalBytes=Encode(new RemovalJournal{SaveRevision=p.SaveRevision,Paths=p.Removed.ToList(),Identities=p.RemovedIdentities.ToList(),ActivePaths=p.Apps.Select(a=>a.Path).ToList()});string journalPath=Path.Combine(Folder,"removed.json");if(!Matches(journalPath,journalBytes))Atomic(journalPath,journalBytes);}catch(Exception ex){Log(ex);}
            if(unchanged)return;
            string inventory=BoardEditState.Capture(p).Signature();
            if(inventory!=lastInventory||(DateTime.UtcNow-lastSnapshot).TotalSeconds>=60)try{
                string folder=Path.Combine(Folder,"snapshots");Directory.CreateDirectory(folder);Atomic(Path.Combine(folder,"layout-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".json"),bytes);
                lastSnapshot=DateTime.UtcNow;lastInventory=inventory;foreach(string old in Directory.GetFiles(folder,"layout-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(30))File.Delete(old);
            }catch(Exception ex){Log(ex);}
        }}
        static bool Matches(string path,byte[] bytes){try{return File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes);}catch{return false;}}
        static Preferences ReadBackupLayout(string path){using(var input=File.OpenRead(path))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){
            var layouts=zip.Entries.Where(e=>e.FullName=="layout.json").ToList();if(layouts.Count!=1||layouts[0].Length>8*1024*1024)throw new InvalidDataException("备份布局不完整或过大");
            using(var entry=layouts[0].Open())using(var memory=new MemoryStream()){var buffer=new byte[16384];int count;while((count=entry.Read(buffer,0,buffer.Length))>0){if(memory.Length+count>8*1024*1024)throw new InvalidDataException("备份布局过大");memory.Write(buffer,0,count);}return ReadLayout(memory.ToArray());}
        }}
        public static List<Entry> HistoricalEntries(){
            var result=new List<Entry>();Directory.CreateDirectory(Folder);var paths=RecoveryPaths().ToList();
            foreach(string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))try{if(File.Exists(path)){var p=ReadLayout(File.ReadAllBytes(path));result.AddRange(p.Apps.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).Select(FeatureRules.Copy));}}catch{}
            string backups=Path.Combine(Folder,"backups");if(Directory.Exists(backups))foreach(string path in Directory.GetFiles(backups,"*.gbbackup").OrderByDescending(File.GetLastWriteTimeUtc).Take(20))try{var p=ReadBackupLayout(path);result.AddRange(p.Apps.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).Select(FeatureRules.Copy));}catch{}
            return EntryRules.NormalizeEntries(result);
        }
    }
    public static class EntryRecoveryRules {
        public static List<Entry> Candidates(Preferences prefs,IEnumerable<Entry> historical){return EntryRules.NormalizeEntries(historical.Select(FeatureRules.Copy)).Where(a=>{var current=prefs.Apps.FirstOrDefault(x=>string.Equals(x.Path,a.Path,StringComparison.OrdinalIgnoreCase));return current==null||FeatureRules.Memberships(a).Any(g=>!FeatureRules.InGroup(current,g));}).ToList();}
        public static int Restore(Preferences prefs,IEnumerable<Entry> chosen){int count=0;foreach(var item in chosen){var current=prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,item.Path,StringComparison.OrdinalIgnoreCase));if(current==null){current=FeatureRules.Copy(item);prefs.Apps.Add(current);}else foreach(string group in FeatureRules.Memberships(item))FeatureRules.AddMembership(current,group);RemovalRules.Forget(prefs,current);count++;}prefs.Normalize();return count;}
    }
}
