import { useEffect, useMemo, useRef, useState } from "react"
import { useWs } from "../hooks/useWs"
import { useEffectAsync } from "../hooks/useEffectAsync"
import './WebRtcTest.css'

const WebRtcTest = () => {

    const onMessage = (message: any) => {
        console.log(message)
    }

    const pc = useMemo(() => new RTCPeerConnection(), [])
    const ws = useWs(onMessage)

    const videoRef = useRef<HTMLVideoElement>(null)

    const [mediaStream, setMediaStream] = useState<MediaStream | null>(null)
    useEffectAsync(async () => {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true })
        setMediaStream(stream)
        stream.getTracks().forEach(track => {
            if (track.kind === 'video') {
                videoRef.current!.srcObject = stream
            }
        })
    }, [])

    useEffectAsync(async () => {
        if (ws && mediaStream) {

            mediaStream.getTracks().forEach(track => pc.addTrack(track, mediaStream))
            const offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true,
            })
            await pc.setLocalDescription(offer)

            ws.send(JSON.stringify({
                type: 'offer',
                sdp: offer.sdp,
                timestamp: new Date().toISOString()
            }))

            ws.onmessage = (event) => {
                const message = JSON.parse(event.data)
                if (message.type === 'answer') {
                    pc.setRemoteDescription(message.answer)
                }
            }
        }
    }, [ws, mediaStream])


    useEffect(() => {
        navigator.mediaDevices.getUserMedia({ video: true, audio: true }).then(stream => {
            stream.getTracks().forEach(track => pc.addTrack(track, stream));
        });
    }, []);
	return <div>
        <div>WebRtcTest</div>
        <video ref={videoRef} autoPlay playsInline />
    </div>
}

export default WebRtcTest

