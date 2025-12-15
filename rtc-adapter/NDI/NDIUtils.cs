namespace Plml.RtcAdapter.NDI;

public static class NDIUtils
{
    public static int CalculateStride(int width, int bpp, int alignment = 8)
    {
        return (width * bpp + alignment - 1) / alignment * alignment;
    }
}