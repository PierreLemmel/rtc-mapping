import { useEffectAsync } from "./useEffectAsync"
import { useCallback, useEffect, useMemo, useState } from "react"

import { getSettings } from '../lib/settings'

export const useRtc = () => {
    const pc = useMemo(() => {
        const { iceServers, iceCandidatePoolSize } = getSettings()
        const pc = new RTCPeerConnection({
            iceServers: iceServers.map(server => ({
                urls: server
            })),
            iceCandidatePoolSize
        })
        return pc
    }, [])

    const [sdpOffer, setSdpOffer] = useState<string | null>(null)
    const [sdpAnswer, setSdpAnswer] = useState<string | null>(null)
    
    const [localTracks, setLocalTracks] = useState<MediaStreamTrack[]>([])
    const [remoteTracks, setRemoteTracks] = useState<MediaStreamTrack[]>([])
    
    const [dataChannels, setDataChannels] = useState<RTCDataChannel[]>([])

    const [connected, setConnected] = useState(false)
    
    useEffect(() => {

        setSdpOffer(null)

        pc.ondatachannel = (event) => {
            console.log('ondatachannel')
            console.log(event)
        }

        pc.ontrack = (event) => {
            console.log('ontrack')
            console.log(event)
        }

        pc.onconnectionstatechange = (event) => {
            const connection = event.target as RTCPeerConnection
            const state = connection.connectionState

            switch (state) {
                case 'connected':
                    setConnected(true)
                    break;
                case 'closed':
                    setConnected(false)
                    break
                default:
                    console.log('connection state changed', state)
                    break
            }
        }
    }, [pc])

    useEffectAsync(async () => {
        console.log(sdpOffer)
        if (sdpOffer) {
            await pc.setRemoteDescription(new RTCSessionDescription({
                type: 'offer',
                sdp: sdpOffer
            }))
            const answer = await pc.createAnswer()
            await pc.setLocalDescription(answer)

            if (answer.type !== 'answer') {
                console.error('SDP answer is not an answer')
                return
            }

            if (!answer.sdp) {
                console.error('No SDP in answer')
                return
            }
            console.log('SDP answer created')
            setSdpAnswer(answer.sdp)
        }
    }, [sdpOffer])
    
    return {
        hasReceivedOffer: sdpOffer !== null,
        setOffer: setSdpOffer,
        sdpAnswer,
        addTrack: pc.addTrack,
        dataChannels,
        localTracks,
        remoteTracks,
        connected,
    }
}