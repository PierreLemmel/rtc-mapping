using System.Runtime.InteropServices;

namespace Plml.RtcAdapter.NDI;

public static partial class NDILib
{
    [StructLayout(LayoutKind.Sequential)]
    public struct send_create_t
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string p_ndi_name;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string? p_ndi_groups;

        [MarshalAs(UnmanagedType.U1)]
        public bool clock_video;
        [MarshalAs(UnmanagedType.U1)]
        public bool clock_audio;
    }

    [DllImport(LibName, EntryPoint = "NDIlib_send_create", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SendCreate(ref send_create_t create);

    [DllImport(LibName, EntryPoint = "NDIlib_send_destroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendDestroy(IntPtr send_obj);
}