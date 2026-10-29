using System;using System.IO;using System.Net;using System.Text;using GlassBoard;
public static class UpdateNetworkTests {
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
    static byte[] Bytes(string value){return Encoding.UTF8.GetBytes(value);}
    public static int Main(){
        string manifest="{\"name\":\"GlassBoard_v0.9.7_User.zip\",\"size\":100,\"type\":\"file\",\"download_url\":\"https://raw.githubusercontent.com/SENLIN-SAMA/win-.-/main/GlassBoard_v0.9.7_User.zip\",\"digest\":\"sha256:"+new string('a',64)+"\"}";
        var offer=UpdateService.CheckUsing((url,max)=>{if(url==UpdateRules.Manifest)return Bytes(manifest);throw new WebException("blocked",WebExceptionStatus.Timeout);});Check(offer.Version==new Version(0,9,7,0),"blocked APIs fall back to checksum-bearing raw index");Check(UpdateService.LastDiagnostics.Contains("超时")&&UpdateService.LastDiagnostics.Contains("正常"),"per-channel diagnostics retained on success");
        offer=UpdateService.CheckUsing((url,max)=>{if(url==UpdateRules.ManifestFallback)return Bytes(manifest);throw new WebException("blocked",WebExceptionStatus.ConnectFailure);});Check(offer!=null,"official website index is independent fallback");
        offer=UpdateService.CheckUsing((url,max)=>{if(url.EndsWith("/contents"))return Bytes("["+manifest+"]");if(url.Contains("/releases?"))return Bytes("[]");throw new WebException("not published");});Check(offer!=null,"root ZIP publishing remains compatible without index");
        bool failed=false;try{UpdateService.CheckUsing((url,max)=>{if(url.Contains("/releases?"))return Bytes("[]");throw new WebException("blocked",WebExceptionStatus.NameResolutionFailure);});}catch(IOException ex){failed=ex.Message.Contains("域名解析失败");}Check(failed,"empty Releases must not mask failed root/index as no update");
        failed=false;try{UpdateService.CheckUsing((url,max)=>{if(url==UpdateRules.Manifest)return Bytes(manifest.Replace(new string('a',64),"invalid"));throw new WebException("blocked");});}catch(IOException){failed=true;}Check(failed,"invalid manifest hash rejected");
        Check(UpdateRules.Alternate("https://raw.githubusercontent.com/SENLIN-SAMA/win-.-/main/GlassBoard_v0.9.7_User.zip").StartsWith("https://github.com/SENLIN-SAMA/win-.-/raw/refs/heads/main/")&&UpdateRules.Alternate("https://attacker.invalid/x")==null,"download fallback confined to same official repository");
        Check(UpdateService.Explain(new WebException("x",WebExceptionStatus.TrustFailure)).Contains("证书")&&UpdateService.Explain(new WebException("x",WebExceptionStatus.ProxyNameResolutionFailure)).Contains("域名"),"certificate and proxy errors distinguishable");
        Console.WriteLine("PASS: real update source fallback/selection, root ZIP compatibility, empty Releases failure handling, checksum rejection, constrained download alternate and network diagnostics.");return 0;
    }
}
