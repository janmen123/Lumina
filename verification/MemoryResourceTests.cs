using System;using System.IO;using System.Text;using System.Security.Cryptography;using Lumina;
class MemoryResourceTests {
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static int Main(){
        var cache=new BoundedCache<string,byte[]>(192,StringComparer.OrdinalIgnoreCase);byte[] seen;
        for(int i=0;i<5000;i++)cache.Put("icon-"+i,new byte[16384]);Check(cache.Count==192,"icon cache holds at most 192 entries after 5000 inserts");
        Check(!cache.TryGet("icon-0",out seen)&&cache.TryGet("ICON-4999",out seen)&&seen.Length==16384,"case-insensitive cache hits and eviction");
        cache.Clear();Check(cache.Count==0&&!cache.TryGet("icon-4999",out seen),"clear releases cache references");
        var lru=new BoundedCache<string,string>(2);string value;lru.Put("a","A");lru.Put("b","B");Check(lru.TryGet("a",out value),"cache touch");lru.Put("c","C");Check(!lru.TryGet("b",out value)&&lru.TryGet("a",out value),"recent icon stays cached");lru.Put("a","changed");Check(lru.Count==2&&lru.TryGet("a",out value)&&value=="changed","replace without growing");
        foreach(var size in new[]{new[]{600,600},new[]{600,6000},new[]{6000,600},new[]{64,64},new[]{1,12000}}){var fit=ImageDecodeRules.Fit(size[0],size[1],600);Check(fit[0]<=600&&fit[1]<=600&&fit[0]>=1&&fit[1]>=1&&fit[0]<=size[0]&&fit[1]<=size[1],"long-edge decode bounds and no upscaling");}
        foreach(string text in new[]{"", "same cover",new string('A',4097),new string('A',2047)+"中文🎵","中文🎵",new string('A',2047)+"🎵"})using(var sha=SHA256.Create()){string expected=BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(text)));Check(CoverFingerprint.Of(text)==expected,"chunked cover fingerprint matches exact full-input SHA256");}
        var stream=new ReleasingReadStream(new byte[]{1,2,3});Check(stream.ReadByte()==1&&stream.CanRead,"image stream reads before decode completes");stream.Dispose();Check(!stream.CanRead&&!stream.CanSeek,"image stream drops its backing buffer on disposal");try{long ignored=stream.Length;throw new Exception("disposed stream remained usable");}catch(ObjectDisposedException){}
        Console.WriteLine("PASS: 5000-icon bounded/LRU cache, thumbnail aspect limits/no upscale, exact constant-buffer cover fingerprints and releasing image streams.");return 0;
    }
}
