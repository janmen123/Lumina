using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;using System.Xml.Linq;
namespace Lumina {
    // The same Windows WLAN calls previously used by the large bridge, hosted
    // in .NET Framework. No redistributable runtime or third-party WLAN library.
    public static class NativeWireless {
        [DllImport("wlanapi.dll")]static extern uint WlanOpenHandle(uint v,IntPtr r,out uint negotiated,out IntPtr handle);
        [DllImport("wlanapi.dll")]static extern uint WlanCloseHandle(IntPtr h,IntPtr r);
        [DllImport("wlanapi.dll")]static extern uint WlanEnumInterfaces(IntPtr h,IntPtr r,out IntPtr list);
        [DllImport("wlanapi.dll")]static extern uint WlanGetAvailableNetworkList(IntPtr h,ref Guid g,uint flags,IntPtr r,out IntPtr list);
        [DllImport("wlanapi.dll")]static extern uint WlanScan(IntPtr h,ref Guid g,IntPtr ssid,IntPtr ie,IntPtr r);
        [DllImport("wlanapi.dll")]static extern uint WlanDisconnect(IntPtr h,ref Guid g,IntPtr r);
        [DllImport("wlanapi.dll",CharSet=CharSet.Unicode)]static extern uint WlanSetProfile(IntPtr h,ref Guid g,uint flags,string xml,string security,bool overwrite,IntPtr reserved,out uint reason);
        [DllImport("wlanapi.dll")]static extern uint WlanConnect(IntPtr h,ref Guid g,ref Connection parameters,IntPtr r);
        [DllImport("wlanapi.dll")]static extern void WlanFreeMemory(IntPtr p);
        [DllImport("wlanapi.dll")]static extern uint WlanQueryInterface(IntPtr handle,ref Guid id,int opcode,IntPtr reserved,out int size,out IntPtr data,out int type);
        [DllImport("wlanapi.dll")]static extern uint WlanSetInterface(IntPtr handle,ref Guid id,int opcode,int size,IntPtr data,IntPtr reserved);
        public static RadioSnapshot[] Radios(){using(var handle=new Handle()){
            var result=new List<RadioSnapshot>();foreach(Guid item in Interfaces(handle.Value)){Guid id=item;int size,type;IntPtr data;uint code=WlanQueryInterface(handle.Value,ref id,4,IntPtr.Zero,out size,out data,out type);
                if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);try{if(size<4)throw new InvalidDataException("无线开关状态长度无效");int count=Marshal.ReadInt32(data);if(count<0||count>64||size<4+12*count)throw new InvalidDataException("无线开关状态列表无效");bool on=false,available=false;for(int i=0;i<count;i++){int at=4+12*i;bool hardware=Marshal.ReadInt32(data,at+8)==1;available|=hardware;on|=hardware&&Marshal.ReadInt32(data,at+4)==1;}result.Add(new RadioSnapshot{Kind="WiFi",Name="Wi-Fi",Enabled=on,Available=available});}finally{WlanFreeMemory(data);}}
            return result.ToArray();}}
        public static void SetRadio(bool enabled){using(var handle=new Handle()){var items=Interfaces(handle.Value);if(items.Count==0)throw new InvalidOperationException("未检测到 Wi-Fi 适配器");IntPtr data=Marshal.AllocHGlobal(12);try{Marshal.WriteInt32(data,0,-1);Marshal.WriteInt32(data,4,enabled?1:2);Marshal.WriteInt32(data,8,0);foreach(Guid item in items){Guid id=item;Check(WlanSetInterface(handle.Value,ref id,4,12,data,IntPtr.Zero));}}finally{Marshal.FreeHGlobal(data);}}}

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Connection {public int Mode;[MarshalAs(UnmanagedType.LPWStr)]public string Profile;public IntPtr Ssid,Bssid;public int Bss;public uint Flags;}
        sealed class Handle:IDisposable {
            public IntPtr Value;
            public Handle(){uint negotiated;Check(WlanOpenHandle(2,IntPtr.Zero,out negotiated,out Value));}
            public void Dispose(){if(Value!=IntPtr.Zero){WlanCloseHandle(Value,IntPtr.Zero);Value=IntPtr.Zero;}}
        }
        static void Check(uint code){if(code!=0)throw new System.ComponentModel.Win32Exception((int)code);}
        static List<Guid> Interfaces(IntPtr handle){IntPtr list;Check(WlanEnumInterfaces(handle,IntPtr.Zero,out list));try{int count=Marshal.ReadInt32(list);if(count<0||count>64)throw new InvalidDataException("无线网卡列表无效");return Enumerable.Range(0,count).Select(i=>(Guid)Marshal.PtrToStructure(IntPtr.Add(list,8+i*532),typeof(Guid))).ToList();}finally{WlanFreeMemory(list);}}
        internal static WifiNetwork ReadNetwork(IntPtr item,Guid adapter){
            int length=Marshal.ReadInt32(item,512);if(length<0||length>32)throw new InvalidDataException("无线网络名称长度无效");
            byte[] bytes=new byte[length];Marshal.Copy(IntPtr.Add(item,516),bytes,0,length);int flags=Marshal.ReadInt32(item,620);
            return new WifiNetwork{Name=Encoding.UTF8.GetString(bytes),Profile=(Marshal.PtrToStringUni(item,256)??"").TrimEnd('\0'),Adapter=adapter.ToString(),Signal=Marshal.ReadInt32(item,604),Secure=Marshal.ReadInt32(item,608)!=0,Connected=(flags&1)!=0,Auth=Marshal.ReadInt32(item,612),Cipher=Marshal.ReadInt32(item,616)};
        }
        public static WifiNetwork[] Networks(bool scan){using(var handle=new Handle()){
            var result=new List<WifiNetwork>();foreach(var id in Interfaces(handle.Value)){Guid adapter=id;if(scan)WlanScan(handle.Value,ref adapter,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);IntPtr list;Check(WlanGetAvailableNetworkList(handle.Value,ref adapter,0,IntPtr.Zero,out list));try{int count=Marshal.ReadInt32(list);if(count<0||count>2048)throw new InvalidDataException("无线网络列表无效");for(int i=0;i<count;i++){var network=ReadNetwork(IntPtr.Add(list,8+i*628),adapter);if(network.Name.Length>0)result.Add(network);}}finally{WlanFreeMemory(list);}}return result.ToArray();
        }}
        public static void Disconnect(string adapter){using(var handle=new Handle()){var id=Guid.Parse(adapter);Check(WlanDisconnect(handle.Value,ref id,IntPtr.Zero));}}
        internal static string ProfileXml(string ssid,string profile,string password,int auth,int cipher){
            bool open=auth==1&&cipher==0;if(!open&&(auth!=7||cipher!=4))throw new InvalidOperationException("此网络需要在 Windows Wi-Fi 设置中连接（企业认证或 WPA3）。");
            if(!open&&(password==null||password.Length<8||password.Length>63))throw new ArgumentException("WPA2 密码需要 8–63 个字符。");
            if(string.IsNullOrEmpty(ssid)||Encoding.UTF8.GetByteCount(ssid)>32)throw new ArgumentException("网络名称无效。");
            XNamespace ns="http://www.microsoft.com/networking/WLAN/profile/v1";
            var security=new XElement(ns+"security",new XElement(ns+"authEncryption",new XElement(ns+"authentication",open?"open":"WPA2PSK"),new XElement(ns+"encryption",open?"none":"AES"),new XElement(ns+"useOneX",false)));
            if(!open)security.Add(new XElement(ns+"sharedKey",new XElement(ns+"keyType","passPhrase"),new XElement(ns+"protected",false),new XElement(ns+"keyMaterial",password)));
            return new XElement(ns+"WLANProfile",new XElement(ns+"name",profile),new XElement(ns+"SSIDConfig",new XElement(ns+"SSID",new XElement(ns+"hex",BitConverter.ToString(Encoding.UTF8.GetBytes(ssid)).Replace("-","")),new XElement(ns+"name",ssid))),new XElement(ns+"connectionType","ESS"),new XElement(ns+"connectionMode","manual"),new XElement(ns+"MSM",security)).ToString();
        }
        public static void Connect(string adapter,string ssid,string profile,string password,int auth,int cipher){using(var handle=new Handle()){
            var id=Guid.Parse(adapter);if(string.IsNullOrWhiteSpace(profile)){profile="Lumina-"+Guid.NewGuid().ToString("N");string xml=ProfileXml(ssid,profile,password,auth,cipher);uint reason;Check(WlanSetProfile(handle.Value,ref id,2,xml,null,false,IntPtr.Zero,out reason));}
            var connection=new Connection{Mode=0,Profile=profile,Bss=1};Check(WlanConnect(handle.Value,ref id,ref connection,IntPtr.Zero));
        }}
    }
}
