using System.Runtime.InteropServices;

namespace Plml.RtcAdapter.NDI;

public static partial class NDILib
{
    public const string LibName = "Processing.NDI.Lib.x64.dll";

    public const long send_timecode_synthesize = long.MaxValue;

    static NDILib()
    {
        bool initialized = Initialize();
        if (!initialized)
            throw new Exception("Failed to initialize NDI library");
        
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            bool destroyed = Destroy();
            if (!destroyed)
                throw new Exception("Failed to destroy NDI library");
        };
    }

    [DllImport(LibName, EntryPoint = "NDIlib_initialize", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool Initialize();

    [DllImport(LibName, EntryPoint = "NDIlib_destroy", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool Destroy();
}