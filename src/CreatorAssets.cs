using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
namespace GlassBoard {
    // Client-side protection raises the cost of swapping images; it cannot secure published source against recompilation.
    internal static class CreatorAssets {
        const string Key = "+w2G9GqN0Dp2dREGskd8qkOktn8hoPuUTF5BN3E70+o=";
        static string[] Identity(string key) {
            if(key=="Wechat")return new[]{"GlassBoard.Assets.c1.dat","62c5159608df0606563bbb8b226f85665aacd2ec4780cff09ca94a4e598458e9","98e4c3c299ff389b1aea9d59045bc674cfa1763772de01000147f0b3b5b84ecd"};
            if(key=="Alipay")return new[]{"GlassBoard.Assets.c2.dat","6752976c421694cb6d5d3a67be3355da1da7f47a4859324efb06cd4295c26ed8","ea068219af9de972ee287fcbaad8897f080b46a6b0aee052f847c88b61420625"};
            if(key=="Douyin")return new[]{"GlassBoard.Assets.c3.dat","9a4f2d1c6a27d964df5206d9ac9f1096dabead7485a872c56f257facf3c1d961","0b03965a7fe841754499fe7736f16c29020f274124789965151967fc80f60124"};
            throw new ArgumentException("未知创作者资源", "key");
        }
        static string Digest(byte[] bytes) {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        internal static byte[] Decode(string key, byte[] payload) {
            var identity=Identity(key);
            if(payload==null || payload.Length<36 || payload.Length>4*1024*1024 || (payload.Length-20)%16!=0 ||
               payload[0]!=71 || payload[1]!=66 || payload[2]!=67 || payload[3]!=49 || Digest(payload)!=identity[1])
                throw new InvalidDataException("创作者收款资源校验失败，请重新解压原版程序。");
            byte[] iv=new byte[16];Buffer.BlockCopy(payload,4,iv,0,16);
            byte[] plain;
            using(var aes=Aes.Create()) {
                aes.Key=Convert.FromBase64String(Key);aes.IV=iv;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;
                using(var decrypt=aes.CreateDecryptor())plain=decrypt.TransformFinalBlock(payload,20,payload.Length-20);
            }
            if(Digest(plain)!=identity[2])throw new InvalidDataException("创作者收款图片校验失败。");
            return plain;
        }
        internal static byte[] Read(string key) {
            var identity=Identity(key);CreatorIdentityProof.VerifyResource(identity[0]);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(identity[0])) {
                if(stream==null || stream.Length>4*1024*1024)throw new InvalidDataException("创作者收款资源缺失或异常。");
                using(var copy=new MemoryStream()){stream.CopyTo(copy);return Decode(key,copy.ToArray());}
            }
        }
    }
}
