using System;using System.IO;using System.Reflection;
class ResourceCheck {
    static MethodInfo decode;
    static void Reject(string key,byte[] bytes){try{decode.Invoke(null,new object[]{key,bytes});}catch(TargetInvocationException ex){if(ex.InnerException is InvalidDataException||ex.InnerException is ArgumentException)return;throw;}throw new Exception("Tampered resource was accepted");}
    static int Main(string[] args){
        var assembly=Assembly.LoadFile(args[0]);var type=assembly.GetType("GlassBoard.CreatorAssets",true);
        var read=type.GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Static);
        decode=type.GetMethod("Decode",BindingFlags.NonPublic|BindingFlags.Static);
        foreach(var key in new[]{"Wechat","Alipay"}){
            byte[] original=File.ReadAllBytes(args[key=="Wechat"?1:2]);
            var actual=(byte[])read.Invoke(null,new object[]{key});
            if(!Equal(actual,original))throw new Exception("Decoded image changed: "+key);
            string name="GlassBoard.Assets."+(key=="Wechat"?"c1.dat":"c2.dat");byte[] payload;
            using(var stream=assembly.GetManifestResourceStream(name))using(var copy=new MemoryStream()){stream.CopyTo(copy);payload=copy.ToArray();}
            if(Equal(payload,original))throw new Exception("Resource is not encrypted");
            if(!Equal((byte[])decode.Invoke(null,new object[]{key,payload}),original))throw new Exception("Decode mismatch");
            var damaged=(byte[])payload.Clone();damaged[damaged.Length/2]^=1;Reject(key,damaged);
            damaged=(byte[])payload.Clone();damaged[4]^=1;Reject(key,damaged);
            damaged=(byte[])payload.Clone();damaged[0]=0;Reject(key,damaged);
            Reject(key,new byte[0]);Reject(key,null);Reject(key,new byte[payload.Length-1]);
            Reject(key=="Wechat"?"Alipay":"Wechat",payload);Reject("Other",payload);
            if(assembly.GetManifestResourceStream("GlassBoard.Creator."+key)!=null)throw new Exception("Plaintext resource still present");
            Console.WriteLine("PASS: "+key+" decrypts to exact uploaded bytes; altered IV/header/ciphertext, truncation, wrong identity rejected");
        }return 0;
    }
    static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
}
