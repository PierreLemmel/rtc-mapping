using System.Net;
using SIPSorceryMedia.Abstractions;

namespace Plml.RtcAdapter;


public interface IVideoBridge : IDisposable
{
    (bool success, IEnumerable<RawImage> images, string? errorMessage) Decode(IPEndPoint remoteEP, uint timestamp, byte[] payload, VideoFormat format);
}