using Plml.RtcAdapter.NDI;
using SIPSorceryMedia.Abstractions;

namespace Plml.RtcAdapter;

public static class VideoUtils
{
    public static NDILib.FourCC_video_type_e SIPToNDIPixelFormat(VideoPixelFormatsEnum pixelFormat)
    {
        return pixelFormat switch
        {
            VideoPixelFormatsEnum.I420 => NDILib.FourCC_video_type_e.I420,
            VideoPixelFormatsEnum.NV12 => NDILib.FourCC_video_type_e.NV12,
            VideoPixelFormatsEnum.Rgb => NDILib.FourCC_video_type_e.RGBX,
            VideoPixelFormatsEnum.Bgr => NDILib.FourCC_video_type_e.BGRX,
            VideoPixelFormatsEnum.Rgba => NDILib.FourCC_video_type_e.RGBA,
            VideoPixelFormatsEnum.Bgra => NDILib.FourCC_video_type_e.BGRA,
            _ => throw new ArgumentException($"Unsupported pixel format: {pixelFormat}"),
        };
    }

    public static int GetBytesPerPixel(VideoPixelFormatsEnum pixelFormat)
    {
        return pixelFormat switch
        {
            VideoPixelFormatsEnum.I420 => 1,
            VideoPixelFormatsEnum.NV12 => 1,
            VideoPixelFormatsEnum.Rgb => 3,
            VideoPixelFormatsEnum.Bgr => 3,
            VideoPixelFormatsEnum.Rgba => 4,
            VideoPixelFormatsEnum.Bgra => 4,
            _ => throw new ArgumentException($"Unsupported pixel format: {pixelFormat}"),
        };
    }

    public static int CalculateStride(int width, int bpp, int alignment = 8)
    {
        return (width * bpp + alignment - 1) / alignment * alignment;
    }
}