import { useCallback, useEffect, useRef, useState } from "react"
import { useWs } from "../hooks/useWs"
import { useEffectAsync } from "../hooks/useEffectAsync"
import type { ClientAddedData, IncomingMessage } from "../messaging/messages";
import { useRtc } from "../hooks/useRtc";
import { cn } from "../lib/util";


const RtcPage = () => {
    const [showConfig, setShowConfig] = useState(true)
    const [userNameEdit, setUserNameEdit] = useState<string>("")
    const [userName, setUserName] = useState<string | null>(null)

    const [wakeLock, setWakeLock] = useState<WakeLockSentinel | null>(null)
    const [isFullscreen, setIsFullscreen] = useState(false)
    const [videoDevices, setVideoDevices] = useState<MediaDeviceInfo[]>([])
    const [currentDeviceIndex, setCurrentDeviceIndex] = useState(0)

    const requestWakeLock = useCallback(async () => {
        if ('wakeLock' in navigator) {
            try {
                const lock = await navigator.wakeLock.request('screen')
                setWakeLock(lock)
                lock.addEventListener('release', () => setWakeLock(null))
            } catch (err) {
                console.warn('Wake Lock request failed:', err)
            }
        }
    }, [])

    useEffect(() => {
        requestWakeLock()

        const handleVisibilityChange = () => {
            if (document.visibilityState === 'visible') {
                requestWakeLock()
            }
        }
        document.addEventListener('visibilitychange', handleVisibilityChange)

        return () => {
            document.removeEventListener('visibilitychange', handleVisibilityChange)
            wakeLock?.release()
        }
    }, [requestWakeLock])

    const toggleFullscreen = useCallback(async () => {
        if (!document.fullscreenElement) {
            try {
                await videoContainerRef.current!.requestFullscreen()
            } catch (err) {
                console.warn('Fullscreen request failed:', err)
            }
        } else {
            await document.exitFullscreen()
        }
    }, [])

    useEffect(() => {
        const handleFullscreenChange = () => {
            setIsFullscreen(!!document.fullscreenElement)
        }
        document.addEventListener('fullscreenchange', handleFullscreenChange)
        return () => document.removeEventListener('fullscreenchange', handleFullscreenChange)
    }, [])

    const onSubmit = useCallback(() => {
        setShowConfig(false)
        setUserName(userNameEdit)
    }, [userNameEdit])

    const videoContainerRef = useRef<HTMLDivElement>(null)
    const videoRef = useRef<HTMLVideoElement>(null)
    const [tracksInitialized, setTracksInitialized] = useState(false)

    const {
        sdpAnswer,
        setOffer,
        addTrack,
        connected: rtcConnected,
    } = useRtc();


    const [mediaStream, setMediaStream] = useState<MediaStream | null>(null)
    const senderRef = useRef<RTCRtpSender | null>(null)

    useEffectAsync(async () => {
        const devices = await navigator.mediaDevices.enumerateDevices()
        const cameras = devices
            .filter(d => d.kind === 'videoinput')
            .filter(d => !d.label.toLowerCase().includes('virtual'))
            .filter(d => !d.label.toLowerCase().includes('ndi'))
            .filter(d => !d.label.toLowerCase().includes('obs'))
        setVideoDevices(cameras)

        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true })
        setMediaStream(stream)
        stream.getTracks().forEach(track => {
            if (track.kind === 'video') {
                videoRef.current!.srcObject = stream
                senderRef.current = addTrack(track, stream)
            }
        })
        setTracksInitialized(true)
    }, [])

    const switchCamera = useCallback(async () => {
        if (videoDevices.length < 2) return

        const nextIndex = (currentDeviceIndex + 1) % videoDevices.length
        const nextDevice = videoDevices[nextIndex]

        try {
            mediaStream?.getVideoTracks().forEach(track => track.stop())

            const newStream = await navigator.mediaDevices.getUserMedia({
                video: { deviceId: { exact: nextDevice.deviceId } },
                audio: false
            })

            const newVideoTrack = newStream.getVideoTracks()[0]
            videoRef.current!.srcObject = newStream

            if (senderRef.current) {
                await senderRef.current.replaceTrack(newVideoTrack)
            }

            setMediaStream(newStream)
            setCurrentDeviceIndex(nextIndex)
        } catch (err) {
            console.warn('Failed to switch camera:', err)
        }
    }, [videoDevices, currentDeviceIndex, mediaStream])


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
    const { clientId, sendMessage } = useWs(userName, onMessage)

    const initialMessageSentRef = useRef(false);
    useEffect(() => {
        if (sendMessage && clientId && !initialMessageSentRef.current) {
            initialMessageSentRef.current = true;
            sendMessage({ type: 'WaitingRoom', data: clientId })
        }
    }, [clientId, sendMessage])

    useEffect(() => {
        if (sdpAnswer && sendMessage && tracksInitialized) {
            sendMessage({ type: 'SdpAnswer', data: sdpAnswer })
        }
    }, [sdpAnswer, sendMessage, tracksInitialized])



	return <div className={cn(
        "h-screen w-screen overflow-hidden",
        "relative",
        "bg-slate-800 p-6",
        "text-white",
    )}>
        {showConfig && <div className={cn(
            "absolute inset-0 z-10",
            "flex flex-col justify-center items-center gap-6",
            "bg-slate-500"
        )}>
            <div className={cn(
                "text-4xl font-bold mb-4",
            )}>
                Configuration
            </div>
            <div>
                <label className={cn(
                    "block text-sm font-medium text-white mb-2",
                )}>
                    Nom
                </label>
                <input
                    className={cn(
                        "mb-4 p-2 rounded-md text-slate-800 w-64",
                        "border border-slate-300 focus:outline-none focus:ring-2 focus:ring-blue-400",
                        "bg-gray-200"
                    )}
                    type="text"
                    placeholder="Entrez votre nom"
                    value={userNameEdit ?? ''}
                    onChange={e => setUserNameEdit(e.target.value)}
                />
            </div>
            <div
                className={cn(
                    "bg-white text-center font-bold text-slate-800 px-2 py-1 rounded-md",
                    "min-w-24 py-2 px-8",
                    userNameEdit && userNameEdit.length > 0 ? "cursor-pointer hover:bg-slate-200" : 'opacity-50 cursor-not-allowed',
                )}
                onClick={userNameEdit && userNameEdit.length > 0 ? onSubmit : undefined}
            >
                Valider
            </div>
        </div>}


        <div className={cn(
            "absolute inset-4",
            "flex flex-col items-stretch gap-6",
        )}>
            <div className={cn(
                "w-full",
                "flex justify-between items-center",
            )}>
                <div className={cn(
                    "text-xl font-bold",
                )}>Plml Mapping</div>
                <div className="flex items-center gap-2">
                    <div
                        className={cn(
                            "text-2xl text-slate-400",
                            "bg-white/50 text-slate-800 px-2 py-1 rounded-md",
                            "cursor-pointer hover:bg-slate-200/50",
                        )}
                        onClick={() => setShowConfig(true)}
                    >
                        ⚙️
                    </div>
                </div>
            </div>

            <div className={cn(
                "grid grid-cols-2 grid-rows-[auto_1fr_auto] place-items-center gap-4",
                "grow",
            )}>
                <div><b>Nom :</b> {userName || "UNITIALIZED"} <span className="text-sm text-slate-400 ml-2">(ID : {clientId ?? 'No client ID'})</span></div>
                <div><b>Connecté :</b> {rtcConnected ? 'Oui' : 'Non'}</div>
                <div ref={videoContainerRef} className={cn(
                    "relative",
                    "col-span-2 grow h-full",
                    "flex justify-center items-center",
                )}>
                    <video
                        className="-scale-x-100 w-full h-full object-contain relative"
                        ref={videoRef}
                        autoPlay
                        playsInline
                        muted
                    />
                    {videoDevices.length > 1 && (
                        <div
                            className={cn(
                                "absolute bottom-2 left-2",
                                "text-2xl",
                                "bg-black/50 px-2.5 py-1.5 rounded-md",
                                "cursor-pointer hover:bg-slate-200/50",
                                "text-white",
                                "transition-all duration-300",
                            )}
                            onClick={switchCamera}
                            title="Switch camera"
                        >
                            🔄
                        </div>
                    )}
                    <div
                        className={cn(
                            "absolute bottom-2 right-2",
                            "text-2xl text-slate-400",
                            "bg-black/50 px-2.5 py-1.5 rounded-md",
                            "cursor-pointer hover:bg-slate-200/50",
                            "text-white",
                            "transition-all duration-300",
                        )}
                        onClick={toggleFullscreen}
                        title={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                    >
                        {isFullscreen ? "⛶" : "⛶"}
                    </div>
                </div>
                <div><b>Nombre de clients :</b> {clientCount}</div>
                <div><b>Caméra :</b> {videoDevices[currentDeviceIndex]?.label ?? 'No camera'}</div>
            </div>
        </div>
        
        
        
    </div>
};

export default RtcPage;
