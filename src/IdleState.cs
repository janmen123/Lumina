using System;using System.Runtime.InteropServices;
namespace Lumina {
    // 空闲判定：用户离开时降低后台轮询频率，回来后的下一次轮询自动恢复。
    internal static class IdleState {
        [StructLayout(LayoutKind.Sequential)] struct LastInputInfo{public uint Size;public uint Time;}
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInputInfo info);
        [DllImport("kernel32.dll")] static extern uint GetTickCount();
        internal static int Seconds(){
            try{
                var info=new LastInputInfo{Size=(uint)Marshal.SizeOf(typeof(LastInputInfo))};
                if(!GetLastInputInfo(ref info))return 0;
                uint elapsed=unchecked(GetTickCount()-info.Time);
                return (int)(elapsed/1000);
            }catch{return 0;}
        }
    }
}
