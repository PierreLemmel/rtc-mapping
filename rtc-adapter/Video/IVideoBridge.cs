using System.Net;
using SIPSorceryMedia.Abstractions;

namespace Plml.RtcAdapter;


public interface IVideoBridge : IDisposable
{
    List<RawImage>? Decode(IPEndPoint remoteEP, uint timestamp, byte[] payload, VideoFormat format);
}