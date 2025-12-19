using System.Net;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using Plml.RtcAdapter.NDI;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace Plml.RtcAdapter;


public class RtcServerConnection : IDisposable
{

    private readonly Settings settings;
    private readonly ILogger logger;

    private NDISender ndiSender;
    private MyFFmpegVideoEndPoint videoEndpoint;

    private bool disposed = false;

    private string connectionId;

    public RtcServerConnection(Settings settings, string connectionId, ILogger logger)
    {
        this.settings = settings;
        this.logger = logger;
        this.connectionId = connectionId;
        
        ndiSender = new NDISender(Logger.Default, connectionId, [settings.NdiGroup]);
        videoEndpoint = new MyFFmpegVideoEndPoint();
        videoEndpoint.logger = logger;
        
        videoEndpoint.RestrictFormats(format => format.Codec == VideoCodecsEnum.VP8);

        videoEndpoint.OnVideoSinkDecodedSampleFaster += OnVideoSinkDecodedSampleFaster;
    }

    public async Task SetVideoSinkFormat(VideoFormat format)
    {
        videoEndpoint.SetVideoSinkFormat(format);
        logger.Log("RTC", $"Video sink format for connection {connectionId} set: {format.Codec}");
        await videoEndpoint.StartVideo();
    }

    public void HandleVideoFrame(IPEndPoint remoteEP, uint timestamp, byte[] frame, VideoFormat format)
    {
        try
        {
            videoEndpoint.GotVideoFrame(remoteEP, timestamp, frame, format);
        }
        catch (Exception ex)
        {
            logger.Error("SINC", $"Error handling video frame: {ex.Message}");
        }
    }


    private void OnVideoSinkDecodedSampleFaster(RawImage image)
    {
        // int stride = NDIUtils.CalculateStride(image.Width, 32, 8);
        // uint bufferSize = Convert.ToUInt32(stride * height);
        // byte* buffer = (byte*)NativeMemory.Alloc(bufferSize);

        // nativeFrame = new()
        // {
        //     xres = width,
        //     yres = height,
        //     fourCC = videoType,
        //     frame_rate_N = 30,
        //     frame_rate_D = 1,
        //     picture_aspect_ratio = (float)width / height,
        //     frame_format_type = NDILib.frame_format_type_e.progressive,
        //     timecode = NDILib.send_timecode_synthesize,
        //     p_data = buffer,
        //     line_stride_in_bytes = stride,
        //     timestamp = timestamp
        // };

        Console.WriteLine($"Video sink decoded sample faster: {image.Width}x{image.Height} {image.PixelFormat}");
    }

    public void Dispose()
    {
        if (disposed) return;

        ndiSender?.Dispose();
        videoEndpoint?.Dispose();

        disposed = true;
    }
}