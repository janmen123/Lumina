using System;using System.IO;using System.Collections.Generic;using System.Reflection;using System.Runtime.InteropServices;
namespace Lumina {
    internal static class ExplorerWindows {
        static object Get(object target,string name){return target.GetType().InvokeMember(name,BindingFlags.GetProperty,null,target,null);}
        static object Call(object target,string name,params object[] args){return target.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,target,args);}
        static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        internal static Dictionary<long,string> Read(){var result=new Dictionary<long,string>();object shell=null,collection=null;try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));collection=Call(shell,"Windows");int count=Math.Min(128,Convert.ToInt32(Get(collection,"Count")));
            for(int i=0;i<count;i++){object window=null;try{window=Call(collection,"Item",i);if(window==null)continue;string full=Convert.ToString(Get(window,"FullName"));if(!Path.GetFileName(full).Equals("explorer.exe",StringComparison.OrdinalIgnoreCase))continue;
                long handle=Convert.ToInt64(Get(window,"HWND"));Uri uri;if(!Uri.TryCreate(Convert.ToString(Get(window,"LocationURL")),UriKind.Absolute,out uri)||!uri.IsFile)continue;
                result[handle]=EntryPathRules.Normalize(uri.LocalPath);
            }catch{}finally{Release(window);}}
        }catch(Exception ex){Store.Trace("Explorer folder matching: "+ex.Message);}finally{Release(collection);Release(shell);}return result;}
    }
}
