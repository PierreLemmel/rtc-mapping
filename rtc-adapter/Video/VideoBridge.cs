using System.Net;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace Plml.RtcAdapter;

public class VideoBridge : IVideoBridge
{
    private readonly ILogger logger;

    private unsafe AVCodecContext* _encoderContext;

    private unsafe AVCodecContext* decoderContext;

    private AVCodecID _codecID;

    private AVHWDeviceType hwDeviceType;

    private unsafe AVFrame* _frame;

    private unsafe AVFrame* _gpuFrame;

    private VideoFrameConverter? _frameConverter;

    private bool initialized;

    private object decoderLock = new object();

    private bool disposed;


    public VideoBridge(ILogger logger)
    {
        this.logger = logger;
    }

    public (bool success, IEnumerable<RawImage> images, string? errorMessage) Decode(IPEndPoint remoteEP, uint timestamp, byte[] payload, VideoFormat format)
    {
        AVCodecID? aVCodecID = FFmpegConvert.GetAVCodecID(format.Codec);
        if (!aVCodecID.HasValue)
        {
            logger.Error("FFMPEG", $"Codec {format.Codec} is not supported.");
            return (false, Enumerable.Empty<RawImage>(), $"Codec {format.Codec} is not supported.");
        }

        (bool success, List<RawImage> images, string? errorMessage) = Decode_Internal(aVCodecID.Value, payload);
        return (success, images, errorMessage);
    }

    private unsafe void InitialiseDecoder(AVCodecID codecID)
    {
        if (initialized)
            return;

        initialized = true;
        _codecID = codecID;
        AVCodec* ptr = ffmpeg.avcodec_find_decoder(codecID);
        if (ptr == null)
            throw new ApplicationException($"Decoder codec could not be found for {codecID}.");

        decoderContext = ffmpeg.avcodec_alloc_context3(ptr);
        if (decoderContext == null)
            throw new ApplicationException("Failed to allocate decoder codec context.");


        if (hwDeviceType != 0)
        {
            ffmpeg.av_hwdevice_ctx_create(&decoderContext->hw_device_ctx, hwDeviceType, null, null, 0).ThrowExceptionIfError();
        }


        ffmpeg.avcodec_open2(decoderContext, ptr, null).ThrowExceptionIfError();
        logger.Log("FFMPEG", $"Successfully initialised ffmpeg based image decoder: CodecId:[{codecID}] - Name:[{GetNameString(ptr->name)}]");
    }


    private unsafe (bool success, List<RawImage> images, string? errorMessage) Decode_Internal(AVCodecID codecID, byte[] buffer)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(VideoBridge));

        lock (decoderLock)
        {
            AVPacket* ptr = ffmpeg.av_packet_alloc();
            try
            {
                byte[] array = new byte[buffer.Length + 64];
                Buffer.BlockCopy(buffer, 0, array, 0, buffer.Length);
                fixed (byte* data = array)
                {
                    int pdfResult = ffmpeg.av_packet_from_data(ptr, data, array.Length);
                    if (pdfResult < 0)
                    {
                        return (false, new List<RawImage>(), GetErrorMessage(pdfResult));
                    }
                    return Decode_Internal(codecID, ptr);
                }
            }
            finally
            {
                ffmpeg.av_packet_from_data(ptr, (byte*)IntPtr.Zero, 0);
                ffmpeg.av_packet_free(&ptr);
            }
        }
    }

    private unsafe (bool success, List<RawImage> images, string? errorMessage) Decode_Internal(AVCodecID codecID, AVPacket* packet)
    {
        if (!initialized)
        {
            InitialiseDecoder(codecID);
            _frame = ffmpeg.av_frame_alloc();
            _gpuFrame = ffmpeg.av_frame_alloc();
        }

        List<RawImage> result = new();
        if (ffmpeg.avcodec_send_packet(decoderContext, packet) < 0)
        {
            return (false, result, "Failed to send packet to decoder.");
        }

        ffmpeg.av_frame_unref(_frame);
        ffmpeg.av_frame_unref(_gpuFrame);
        int num;
        for (num = ffmpeg.avcodec_receive_frame(decoderContext, _frame); num == 0; num = ffmpeg.avcodec_receive_frame(decoderContext, _frame))
        {
            AVFrame* ptr = _frame;
            if (decoderContext->hw_device_ctx != null)
            {
                int transferResult = ffmpeg.av_hwframe_transfer_data(_gpuFrame, _frame, 0);
                if (transferResult < 0)
                {
                    return (false, result, $"Failed to transfer data from frame to GPU frame: {transferResult}.");
                }
                ptr = _gpuFrame;
            }

            int width = ptr->width;
            int height = ptr->height;

            if (_frameConverter == null || _frameConverter.SourceWidth != width || _frameConverter.SourceHeight != height)
            {
                _frameConverter = new VideoFrameConverter(width, height, (AVPixelFormat)ptr->format, width, height, AVPixelFormat.AV_PIX_FMT_BGRA);
            }

            AVFrame* ptr2 = _frameConverter.Convert(ptr);
            if (ptr2->width != 0 && ptr2->height != 0)
            {
                RawImage item = new RawImage
                {
                    Width = width,
                    Height = height,
                    Stride = ptr2->linesize[0u],
                    Sample = (nint)ptr2->data[0u],
                    PixelFormat = VideoPixelFormatsEnum.Bgra
                };
                result.Add(item);
            }

            ffmpeg.av_frame_unref(_frame);
            ffmpeg.av_frame_unref(_gpuFrame);
        }

        return (true, result, null);
    }

    public unsafe void Dispose()
    {
        disposed = true;
        lock (this)
        {
            if (_encoderContext != null)
            {
                fixed (AVCodecContext** avctx = &_encoderContext)
                {
                    ffmpeg.avcodec_free_context(avctx);
                }
            }

            if (decoderContext != null)
            {
                fixed (AVCodecContext** avctx = &decoderContext)
                {
                    ffmpeg.avcodec_free_context(avctx);
                }
            }

            if (_frame != null)
            {
                fixed (AVFrame** frame = &_frame)
                {
                    ffmpeg.av_frame_free(frame);
                }
            }

            if (_gpuFrame != null)
            {
                fixed (AVFrame** frame = &_gpuFrame)
                {
                    ffmpeg.av_frame_free(frame);
                }
            }
        }
    }

    private unsafe static string? GetNameString(byte* name) => Marshal.PtrToStringAnsi((nint)name);

    
    public unsafe static string GetErrorMessage(int error)
    {
        int num = 1024;
        byte* ptr = stackalloc byte[(int)(uint)num];
        ffmpeg.av_strerror(error, ptr, (ulong)num);
        return Marshal.PtrToStringAnsi((nint)ptr) ?? "Unknown error";
    }
}