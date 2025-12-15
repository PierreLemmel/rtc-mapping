namespace Plml.RtcAdapter.NDI;

public interface INDISender : IDisposable
{
    string Name { get; }

    void SendFrame(NDIVideoFrame frame);
}