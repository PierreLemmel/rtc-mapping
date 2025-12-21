using System.Net;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace Plml.RtcAdapter;

public class VideoBridge : IVideoBridge
{
    private readonly ILogger logger;
    private readonly FFmpegVideoEncoder ffmpegDecoder;

    private bool disposed = false;

    public VideoBridge(ILogger logger, FFmpegVideoEncoder ffmpegDecoder)
    {
        this.logger = logger;
        this.ffmpegDecoder = ffmpegDecoder;
    }

    public List<RawImage>? Decode(IPEndPoint remoteEP, uint timestamp, byte[] payload, VideoFormat format)
    {
        AVCodecID? aVCodecID = FFmpegConvert.GetAVCodecID(format.Codec);
        if (!aVCodecID.HasValue)
        {
            logger.Error("DECODE", $"Codec {format.Codec} is not supported.");
            return null;
        }

        List<RawImage>? list = ffmpegDecoder.DecodeFaster(aVCodecID.Value, payload, out int width, out int height);
        if (list == null || width == 0 || height == 0)
        {
            logger.Warn("DECODE", $"Decode of video sample failed, width {width}, height {height}.");
            return null;
        }

        return list;
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        ffmpegDecoder.Dispose();
    }
}