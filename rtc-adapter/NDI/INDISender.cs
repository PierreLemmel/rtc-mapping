namespace Plml.RtcAdapter.NDI;

public interface INDISender : IDisposable
{
    string Name { get; }

    void SendFrame(NDILib.video_frame_v2_t frame);
}