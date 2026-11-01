using System;using System.Collections.Generic;using System.IO;using System.Text;using System.Security.Cryptography;using System.Windows.Media.Imaging;
namespace Lumina {
    public sealed class BoundedCache<TKey,TValue> {
        sealed class Item {public TKey Key;public TValue Value;}
        readonly Dictionary<TKey,LinkedListNode<Item>> lookup;readonly LinkedList<Item> recent=new LinkedList<Item>();readonly int capacity;
        public BoundedCache(int limit,IEqualityComparer<TKey> comparer=null){capacity=Math.Max(1,limit);lookup=new Dictionary<TKey,LinkedListNode<Item>>(comparer??EqualityComparer<TKey>.Default);}
        public int Count{get{return lookup.Count;}}
        public bool TryGet(TKey key,out TValue value){LinkedListNode<Item> node;if(!lookup.TryGetValue(key,out node)){value=default(TValue);return false;}recent.Remove(node);recent.AddFirst(node);value=node.Value.Value;return true;}
        public void Put(TKey key,TValue value){LinkedListNode<Item> node;if(lookup.TryGetValue(key,out node)){node.Value.Value=value;recent.Remove(node);recent.AddFirst(node);return;}node=recent.AddFirst(new Item{Key=key,Value=value});lookup.Add(key,node);if(lookup.Count>capacity){var tail=recent.Last;recent.RemoveLast();lookup.Remove(tail.Value.Key);}}
        public void Clear(){lookup.Clear();recent.Clear();}
    }
    public static class ImageDecodeRules {
        public static int[] Fit(int width,int height,int limit){if(width<1||height<1||limit<1)throw new ArgumentOutOfRangeException();double ratio=Math.Min(1,(double)limit/Math.Max(width,height));return new[]{Math.Max(1,(int)Math.Round(width*ratio)),Math.Max(1,(int)Math.Round(height*ratio))};}
    }
    public static class CoverFingerprint {
        public static string Of(string text){using(var hash=SHA256.Create()){var block=new byte[4096];for(int offset=0;offset<text.Length;offset+=2048){int chars=Math.Min(2048,text.Length-offset);for(int i=0;i<chars;i++){char c=text[offset+i];block[i*2]=(byte)c;block[i*2+1]=(byte)(c>>8);}hash.TransformBlock(block,0,chars*2,block,0);}hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash);}}
    }
    sealed class ReleasingReadStream:Stream {
        Stream inner;
        public ReleasingReadStream(byte[] bytes){inner=new MemoryStream(bytes,false);}
        Stream Current{get{if(inner==null)throw new ObjectDisposedException("ReleasingReadStream");return inner;}}
        public override bool CanRead{get{return inner!=null;}}public override bool CanSeek{get{return inner!=null;}}public override bool CanWrite{get{return false;}}
        public override long Length{get{return Current.Length;}}public override long Position{get{return Current.Position;}set{Current.Position=value;}}
        public override int Read(byte[] buffer,int offset,int count){return Current.Read(buffer,offset,count);}public override long Seek(long offset,SeekOrigin origin){return Current.Seek(offset,origin);}
        public override void Flush(){}public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
        protected override void Dispose(bool disposing){var stream=inner;inner=null;if(disposing&&stream!=null)stream.Dispose();base.Dispose(disposing);}
    }
    public static class ThumbnailImages {
        public static BitmapImage Read(byte[] bytes,int maxEdge){
            using(var stream=new ReleasingReadStream(bytes)){
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];int width=frame.PixelWidth,height=frame.PixelHeight;
                var fit=ImageDecodeRules.Fit(width,height,maxEdge);stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
                if(fit[0]!=width||fit[1]!=height){if(width>=height)image.DecodePixelWidth=fit[0];else image.DecodePixelHeight=fit[1];}
                image.StreamSource=stream;image.EndInit();image.Freeze();return image;
            }
        }
    }
}
