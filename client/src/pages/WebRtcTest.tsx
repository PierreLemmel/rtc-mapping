import { useEffect, useMemo, useRef, useState } from "react"
import { useWs } from "../hooks/useWs"
import { useEffectAsync } from "../hooks/useEffectAsync"
import './WebRtcTest.css'

const WebRtcTest = () => {

    const onMessage = (message: any) => {
        console.log(message)
    }

    const { clientId, isConnected, sendMessage } = useWs(onMessage)

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

    const [peerConnection, setPeerConnection] = useState<RTCPeerConnection | null>(null)
    useEffectAsync(async () => {
        if (isConnected && sendMessage && mediaStream) {

            const pc = new RTCPeerConnection()
            mediaStream.getTracks().forEach(track => pc.addTrack(track, mediaStream))
            const offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true,
            })
            await pc.setLocalDescription(offer)

            setPeerConnection(pc)
console.log(offer)
            sendMessage({
                type: 'offer',
                data: offer.sdp,
            })
        }
    }, [isConnected, sendMessage, mediaStream])

	return <div>
        <div>WebRtcTest</div>
        <div>Client ID: {clientId ?? 'No client ID'}</div>
        <div>Is Connected: {isConnected ? 'Yes' : 'No'}</div>
        {sendMessage && <div>Send Message: <button onClick={() => sendMessage({ type: 'log', data: 'Kikou' })}>Send Message</button></div>}
        <video ref={videoRef} autoPlay playsInline muted />
    </div>
};

export default WebRtcTest;
