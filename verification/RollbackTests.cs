using System;using System.IO;using System.Text;using System.Runtime.Serialization.Json;using Lumina;
class RollbackTests {
 static int checks;static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static T Read<T>(string json){using(var s=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(s);}
 static T Round<T>(T v){using(var s=new MemoryStream()){var j=new DataContractJsonSerializer(typeof(T));j.WriteObject(s,v);s.Position=0;return (T)j.ReadObject(s);}}
 static int Main(string[] args){
  var old=Read<Preferences>("{\"SystemGlass\":{\"Explorer\":true,\"StartMenu\":true,\"ExplorerTint\":88},\"Apps\":[{\"Name\":\"保留软件\",\"Path\":\"keep.exe\",\"Pinned\":true}],\"Groups\":[\"全部\",\"工作\"],\"AllDesktopLayer\":true}");old.Normalize();Check(old.Apps.Count==1&&old.Apps[0].Pinned&&old.Apps[0].Name=="保留软件","rollback retains configured apps");Check(old.AllDesktopLayer&&old.Groups.Contains("工作"),"rollback retains desktop layer and groups");
  using(var output=new MemoryStream()){new DataContractJsonSerializer(typeof(Preferences)).WriteObject(output,old);string json=Encoding.UTF8.GetString(output.ToArray());Check(!json.Contains("SystemGlass"),"retired feature settings are not re-saved");}
  Check(typeof(Preferences).GetField("SystemGlass")==null,"retired feature is absent from production preferences");
  byte[] payload=Convert.FromBase64String(CreatorIdentityProof.Payload64),signature=Convert.FromBase64String(CreatorIdentityProof.Signature64);Check(CreatorIdentityProof.VerifySignature(payload,signature),"official branding signature");payload[0]^=1;Check(!CreatorIdentityProof.VerifySignature(payload,signature),"modified identity rejected");payload[0]^=1;signature[3]^=1;Check(!CreatorIdentityProof.VerifySignature(payload,signature),"modified signature rejected");
  byte[] encoded=File.ReadAllBytes(Path.Combine(args[0],"assets","support","c3.dat"));byte[] image=CreatorAssets.Decode("Douyin",encoded);Check(image.Length>100&&image[0]==255&&image[1]==216,"encrypted Douyin resource decodes as JPEG");encoded[24]^=1;bool rejected=false;try{CreatorAssets.Decode("Douyin",encoded);}catch(InvalidDataException){rejected=true;}Check(rejected,"tampered encrypted resource rejected before decoding");
  Console.WriteLine("RollbackTests: "+checks+" checks PASS: legacy data retained, removed feature stays removed, creator signatures and encrypted assets intact.");return 0;
 }
}
