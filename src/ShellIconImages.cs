using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lumina {
    public static class ShellIconImages {
        [ComImport,Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageList {
            [PreserveSig] int Add(IntPtr image,IntPtr mask,out int index);
            [PreserveSig] int ReplaceIcon(int index,IntPtr icon,out int result);
            [PreserveSig] int SetOverlayImage(int index,int overlay);
            [PreserveSig] int Replace(int index,IntPtr image,IntPtr mask);
            [PreserveSig] int AddMasked(IntPtr image,uint color,out int index);
            [PreserveSig] int Draw(IntPtr parameters);
            [PreserveSig] int Remove(int index);
            [PreserveSig] int GetIcon(int index,uint flags,out IntPtr icon);
        }
        [DllImport("shell32.dll",EntryPoint="SHGetImageList")] static extern int SHGetImageList(int size,ref Guid id,out IImageList list);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        // A bounded 128px bitmap keeps small launcher icons sharp at common DPI
        // settings without retaining a full 256px image in every cache entry.
        public static BitmapSource Read(int index,uint overlayMask=0){
            foreach(int size in new[]{4,2}) { // SHIL_JUMBO (256), SHIL_EXTRALARGE (48).
                IImageList list=null;IntPtr icon=IntPtr.Zero;
                try{
                    var id=typeof(IImageList).GUID;if(SHGetImageList(size,ref id,out list)<0||list==null||list.GetIcon(index,1|overlayMask,out icon)<0||icon==IntPtr.Zero)continue;
                    var source=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();
                    // Some shell handlers put a tiny icon inside a 256px canvas.
                    // Fall back to the normal 48px list rather than showing it tiny.
                    if(size==4&&TinyFootprint(source))continue;
                    return Limit(source,128);
                }catch{}finally{if(icon!=IntPtr.Zero)DestroyIcon(icon);if(list!=null&&Marshal.IsComObject(list))Marshal.ReleaseComObject(list);}
            }
            return null;
        }
        static bool TinyFootprint(BitmapSource source){
            if(source.PixelWidth<128||source.PixelHeight<128)return false;
            var pixels=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int width=pixels.PixelWidth,height=pixels.PixelHeight,stride=width*4;
            var bytes=new byte[stride*height];pixels.CopyPixels(bytes,stride,0);int left=width,top=height,right=-1,bottom=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(bytes[y*stride+x*4+3]>16){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            return right<0||Math.Max(right-left+1,bottom-top+1)<96;
        }
        static BitmapSource Limit(BitmapSource source,int maximum){
            if(Math.Max(source.PixelWidth,source.PixelHeight)<=maximum)return source;
            double scale=(double)maximum/Math.Max(source.PixelWidth,source.PixelHeight);int width=Math.Max(1,(int)Math.Round(source.PixelWidth*scale)),height=Math.Max(1,(int)Math.Round(source.PixelHeight*scale));
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);using(var drawing=visual.RenderOpen())drawing.DrawImage(source,new Rect(0,0,width,height));
            var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
        }
    }
}
