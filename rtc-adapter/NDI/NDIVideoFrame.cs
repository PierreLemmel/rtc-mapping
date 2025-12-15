using System.Runtime.InteropServices;

namespace Plml.RtcAdapter.NDI;


public class NDIVideoFrame : IDisposable
{
    internal NDILib.video_frame_v2_t nativeFrame;

    private bool disposed = false;

    public int Width => nativeFrame.xres;
    public int Height => nativeFrame.yres;

    public int Stride => nativeFrame.line_stride_in_bytes;
    public unsafe byte* Data => nativeFrame.p_data;

    public unsafe NDIVideoFrame(int width, int height)
    {
        int stride = NDIUtils.CalculateStride(width, 32, 8);
        uint bufferSize = Convert.ToUInt32(stride * height);
        byte* buffer = (byte*)NativeMemory.Alloc(bufferSize);

        nativeFrame = new()
        {
            xres = width,
            yres = height,
            fourCC = NDILib.FourCC_video_type_e.RGBA,
            frame_rate_N = 30,
            frame_rate_D = 1,
            picture_aspect_ratio = (float)width / height,
            frame_format_type = NDILib.frame_format_type_e.progressive,
            timecode = NDILib.send_timecode_synthesize,
            p_data = buffer,
            line_stride_in_bytes = stride,
            p_metadata = "<hello />",
            timestamp = 0
        };
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        
    }

    ~NDIVideoFrame()
    {
        Dispose();
    }
}