using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Plml.RtcAdapter.NDI;

public class NDISender : INDISender
{
    public string Name { get; init; }
    public string[] Groups { get; init; }
    private bool disposed = false;

    private readonly ILogger logger;
    
    private IntPtr send_handle;

    public NDISender(ILogger logger, string name, string[] groups)
    {
        Name = name;
        Groups = groups;
        this.logger = logger;

        string groupsString = string.Join(",", groups);
        NDILib.send_create_t create = new()
        {
            p_ndi_name = name,
            p_ndi_groups = groupsString,
            clock_video = true,
            clock_audio = false
        };

        IntPtr result = NDILib.SendCreate(ref create);

        if (result == IntPtr.Zero)
        {
            logger.Error("NDI", $"Failed to create NDI sender: {Marshal.GetLastWin32Error()}");
            return;
        }

        logger.Log("NDI", $"Created NDI sender: '{Name}' in group {string.Join(", ", groupsString.Split(',').Select(g => $"'{g}'"))}");
        send_handle = result;
    }

    public void SendFrame(NDILib.video_frame_v2_t frame)
    {
        NDILib.SendVideoSync(send_handle, ref frame);
    }

    ~NDISender()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        if (send_handle != IntPtr.Zero)
        {
            NDILib.SendDestroy(send_handle);
            send_handle = IntPtr.Zero;
        }
        logger.Log("NDI", $"Destroyed NDI sender: '{Name}'");
    }
}