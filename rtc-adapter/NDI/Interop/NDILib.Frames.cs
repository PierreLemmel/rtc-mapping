using System.Runtime.InteropServices;

namespace Plml.RtcAdapter.NDI;

public static partial class NDILib
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct video_frame_v2_t
    {
        public int xres;
        public int yres;
        public FourCC_video_type_e fourCC;
        public int frame_rate_N;
        public int frame_rate_D;
        public float picture_aspect_ratio;
        public frame_format_type_e frame_format_type;
        public long timecode;
        public IntPtr p_data;
        public int line_stride_in_bytes;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string p_metadata;

        public long timestamp;
    }

    public enum FourCC_video_type_e
    {
        UYVY = 0x59565955,
        UYVA = 0x41565955,
        P216 = 0x36313250,
        PA16 = 0x36314150,
        YV12 = 0x32315659,
        I420 = 0x30323449,
        NV12 = 0x3231564E,
        BGRA = 0x41524742,
        BGRX = 0x58524742,
        RGBA = 0x41424752,
        RGBX = 0x58474252,
    }

    public enum frame_format_type_e
    {
        progressive = 1,
        interleaved = 0,
        field_0 = 2,
        field_1 = 3,
    }


    [DllImport(LibName, EntryPoint = "NDIlib_send_send_video_v2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendVideoSync(IntPtr send_handle, ref video_frame_v2_t frame);

    [DllImport(LibName, EntryPoint = "NDIlib_send_send_video_async_v2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendVideoAsync(IntPtr send_handle, ref video_frame_v2_t frame);
}