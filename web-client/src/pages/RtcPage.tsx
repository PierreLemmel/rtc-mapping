import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useWs } from "../hooks/useWs"
import { useEffectAsync } from "../hooks/useEffectAsync"
import './RtcPage.css'
import type { ClientAddedData, IncomingMessage } from "../messaging/messages";
import { useRtc } from "../hooks/useRtc";

const RtcPage = () => {

    const videoRef = useRef<HTMLVideoElement>(null)
    const [tracksInitialized, setTracksInitialized] = useState(false)

    const {
        sdpAnswer,
        setOffer,
        addTrack,
        localTracks,
        remoteTracks,
        connected: rtcConnected,
    } = useRtc();


    const [mediaStream, setMediaStream] = useState<MediaStream | null>(null)
    useEffectAsync(async () => {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true })
        setMediaStream(stream)
        stream.getTracks().forEach(track => {
            if (track.kind === 'video') {
                videoRef.current!.srcObject = stream
                addTrack(track, stream)
            }
        })
        setTracksInitialized(true)
    }, [])


    const [clientCount, setClientCount] = useState<number>(0)

    const onMessage = useCallback((message: IncomingMessage) => {
        switch (message.type) {
            case 'ClientAdded':
                {
                    const { id, count } = JSON.parse(message.data) as ClientAddedData
                    console.log(`Client ${id} added, total clients: ${count}`)
                    setClientCount(count)
                }
                break;
            case 'SdpOffer':
                {
                    const sdpOffer = message.data as string
                    console.log(`SDP offer received`)
                    setOffer(sdpOffer)
                }
                break;
            case 'SdpAnswer':
                break;
            default:
                console.log(`Unknown message type: ${message.type}`)
                break;
        }
    }, [setOffer])
    const { clientId, sendMessage } = useWs(onMessage)

    useEffect(() => {
        if (sdpAnswer && sendMessage && tracksInitialized) {
            sendMessage({ type: 'SdpAnswer', data: sdpAnswer })
        }
    }, [sdpAnswer, sendMessage, tracksInitialized])



	return <div>
        <div>WebRtcTest</div>
        <div>Tracks Initialized: {tracksInitialized ? 'Yes' : 'No'}</div>
        <div>Client ID: {clientId ?? 'No client ID'}</div>
        <div>Client Count: {clientCount}</div>
        <div>RTC Connected: {rtcConnected ? 'Yes' : 'No'}</div>
        <div>Remote Tracks: {remoteTracks.length}</div>
        <div>Local Tracks: {localTracks.length}</div>
        {sendMessage && <div>Send Message: <button onClick={() => sendMessage({ type: 'Log', data: 'Kikou' })}>Send Message</button></div>}
        <video ref={videoRef} autoPlay playsInline muted />
    </div>
};

export default RtcPage;
