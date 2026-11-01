using System;using System.Collections.Generic;using System.ComponentModel;using System.Runtime.InteropServices;
namespace Lumina {
    public static class NativeBluetooth {
        [StructLayout(LayoutKind.Sequential)]struct FindRadio{public uint Size;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct RadioInfo{public uint Size;public ulong Address;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)]public string Name;public uint Class;public ushort Subversion,Manufacturer;}
        [StructLayout(LayoutKind.Sequential)]struct SystemTime{public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Milliseconds;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct DeviceInfo{public uint Size;public ulong Address;public uint Class;public int Connected,Remembered,Authenticated;public SystemTime LastSeen,LastUsed;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)]public string Name;}
        [StructLayout(LayoutKind.Sequential)]struct FindDevice{public uint Size;public int Authenticated,Remembered,Unknown,Connected,Inquiry;public byte Timeout;public IntPtr Radio;}
        [DllImport("BluetoothAPIs.dll",SetLastError=true)]static extern IntPtr BluetoothFindFirstRadio(ref FindRadio parameters,out IntPtr radio);
        [DllImport("BluetoothAPIs.dll",SetLastError=true)]static extern bool BluetoothFindNextRadio(IntPtr find,out IntPtr radio);
        [DllImport("BluetoothAPIs.dll")]static extern bool BluetoothFindRadioClose(IntPtr find);
        [DllImport("BluetoothAPIs.dll")]static extern uint BluetoothGetRadioInfo(IntPtr radio,ref RadioInfo info);
        [DllImport("BluetoothAPIs.dll",SetLastError=true)]static extern IntPtr BluetoothFindFirstDevice(ref FindDevice parameters,ref DeviceInfo info);
        [DllImport("BluetoothAPIs.dll",SetLastError=true)]static extern bool BluetoothFindNextDevice(IntPtr find,ref DeviceInfo info);
        [DllImport("BluetoothAPIs.dll")]static extern bool BluetoothFindDeviceClose(IntPtr find);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        public static RadioSnapshot[] Radios(){var result=new List<RadioSnapshot>();var parameters=new FindRadio{Size=(uint)Marshal.SizeOf(typeof(FindRadio))};IntPtr radio;var find=BluetoothFindFirstRadio(ref parameters,out radio);if(find==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error!=0&&error!=259)throw new Win32Exception(error);return result.ToArray();}try{do{try{var info=new RadioInfo{Size=(uint)Marshal.SizeOf(typeof(RadioInfo))};uint code=BluetoothGetRadioInfo(radio,ref info);if(code!=0)throw new Win32Exception((int)code);result.Add(new RadioSnapshot{Kind="Bluetooth",Name=info.Name,Enabled=true,Available=false});}finally{CloseHandle(radio);}}while(BluetoothFindNextRadio(find,out radio));}finally{BluetoothFindRadioClose(find);}return result.ToArray();}
        public static BluetoothDevice[] Devices(){var result=new List<BluetoothDevice>();var parameters=new FindDevice{Size=(uint)Marshal.SizeOf(typeof(FindDevice)),Authenticated=1,Remembered=1,Connected=1};var info=new DeviceInfo{Size=(uint)Marshal.SizeOf(typeof(DeviceInfo))};var find=BluetoothFindFirstDevice(ref parameters,ref info);if(find==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error!=0&&error!=259)throw new Win32Exception(error);return result.ToArray();}try{do{result.Add(new BluetoothDevice{Id=info.Address.ToString("X12"),Name=info.Name,Paired=info.Authenticated!=0||info.Remembered!=0,Connected=info.Connected!=0});info.Size=(uint)Marshal.SizeOf(typeof(DeviceInfo));}while(BluetoothFindNextDevice(find,ref info));}finally{BluetoothFindDeviceClose(find);}return result.ToArray();}
    }
}
