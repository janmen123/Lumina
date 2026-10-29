using System;using System.IO;using System.Net;using System.Text;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;
namespace GlassBoard {
    public static class WeatherTransport {
        public static T Read<T>(Stream stream){
            if(stream==null)throw new InvalidDataException("天气服务未返回内容");using(var copy=new MemoryStream()){
                byte[] block=new byte[8192];int count;while((count=stream.Read(block,0,block.Length))>0){if(copy.Length+count>2*1024*1024)throw new InvalidDataException("天气响应超过大小限制");copy.Write(block,0,count);}copy.Position=0;
                string text;using(var reader=new StreamReader(copy,new UTF8Encoding(false,true),true,8192,true))text=reader.ReadToEnd();if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("天气服务返回空内容");
                using(var json=new MemoryStream(Encoding.UTF8.GetBytes(text.TrimStart('\uFEFF')))){try{return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(json);}catch(SerializationException ex){throw new InvalidDataException("天气返回内容无法解析为 JSON",ex);}}
            }
        }
        public static HttpWebRequest Request(string url,bool direct){
            Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps&&!(uri.IsLoopback&&uri.Scheme==Uri.UriSchemeHttp))throw new ArgumentException("天气地址需要 HTTPS");
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(uri);
            request.Method="GET";request.Timeout=8000;request.ReadWriteTimeout=8000;request.ProtocolVersion=HttpVersion.Version11;
            request.KeepAlive=false;request.ServicePoint.Expect100Continue=false;request.AllowAutoRedirect=true;request.MaximumAutomaticRedirections=3;
            request.UserAgent="GlassBoard/"+typeof(WeatherTransport).Assembly.GetName().Version.ToString(3);request.Accept="application/json";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);if(direct)request.Proxy=null;return request;
        }
        public static T Get<T>(string url,bool direct){
            for(int attempt=0;;attempt++){
                var request=Request(url,direct);try{using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())return Read<T>(stream);}
                catch(WebException ex){if(ex.Response!=null)ex.Response.Close();bool transient=ex.Status==WebExceptionStatus.ConnectionClosed||ex.Status==WebExceptionStatus.KeepAliveFailure||ex.Status==WebExceptionStatus.ReceiveFailure||ex.Status==WebExceptionStatus.SendFailure;
                    if(attempt==0&&transient)continue;throw;
                }finally{request.Abort();}
            }
        }
    }
}
