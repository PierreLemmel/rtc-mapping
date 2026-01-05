import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useWs, type UserData } from "../hooks/useWs"
import { useEffectAsync } from "../hooks/useEffectAsync"
import type { ClientAddedData, IncomingMessage } from "../messaging/messages";
import { useRtc } from "../hooks/useRtc";
import { cn } from "../lib/util";

type MediaStreamData = {
    deviceId: string;
    stream: MediaStream | null;
}

const RtcPage = () => {
    const [showConfig, setShowConfig] = useState(true)
    const [userNameEdit, setUserNameEdit] = useState<string>("")
    const [userName, setUserName] = useState<string | null>(null)

    const [wakeLock, setWakeLock] = useState<WakeLockSentinel | null>(null)
    const [isFullscreen, setIsFullscreen] = useState(false)
    const [videoDevices, setVideoDevices] = useState<MediaDeviceInfo[] | null>(null)
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
    const [camerasInitialized, setCamerasInitialized] = useState(false)

    const {
        sdpAnswer,
        setOffer,
        addTrack,
        connected: rtcConnected,
    } = useRtc();

    const mediaInitStartedRef = useRef(false);
    const mediaStreamsRef = useRef<MediaStreamData[]>([])
    const videosRefs = useRef<(HTMLVideoElement|null)[]>([])


    useEffect(() => {
        if(!mediaStreamsRef.current) return;

        
    }, [camerasInitialized])

    useEffectAsync(async () => {
        if (mediaInitStartedRef.current) return
        mediaInitStartedRef.current = true
        
        const devices = await navigator.mediaDevices.enumerateDevices()
        const cameras = devices
            .filter(d => d.kind === 'videoinput')
            .filter(d => !d.label.toLowerCase().includes('virtual'))
            .filter(d => !d.label.toLowerCase().includes('ndi'))
            .filter(d => !d.label.toLowerCase().includes('obs'))
        setVideoDevices(cameras)

        mediaStreamsRef.current = cameras.map(cam => {

            return {
                deviceId: cam.deviceId,
                stream: null,
            } satisfies MediaStreamData
        })
        const tasks = mediaStreamsRef.current.map(async (streamData, index) => {

            console.log(`Getting media stream for device ${streamData.deviceId}`)
            const stream = await navigator.mediaDevices.getUserMedia({ video: {
                deviceId: { exact: streamData.deviceId },
            }, audio: true })

            const video = videosRefs.current[index]
            streamData.stream = stream
            stream.getTracks().forEach(track => {
                if (track.kind === 'video') {
                    video!.srcObject = stream
                }
            })

            console.log(`Media stream for device ${streamData.deviceId} initialized`)
        })
        
        await Promise.all(tasks)
        setCamerasInitialized(true)
    }, [])

    const canSwitchCamera = useMemo(() => {
        return videoDevices !== null && videoDevices.length > 1
    }, [videoDevices])
    
    const switchCamera = useCallback(async () => {
        if (videoDevices === null || videoDevices.length < 2) return
        const nextIndex = (currentDeviceIndex + 1) % videoDevices.length
        setCurrentDeviceIndex(nextIndex)
    }, [videoDevices, currentDeviceIndex])


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


    const userData: UserData|null = useMemo(() => {
        if (videoDevices === null) return null
        if (userName === null) return null
        return {
            userName: userName,
            camCount: videoDevices.length,
        }
    }, [userName, videoDevices])
    const { clientId, sendMessage } = useWs(userData, onMessage)

    const initialMessageSentRef = useRef(false);
    useEffect(() => {
        if (sendMessage && clientId && !initialMessageSentRef.current) {
            initialMessageSentRef.current = true;
            sendMessage({ type: 'WaitingRoom', data: clientId })
        }
    }, [clientId, sendMessage])

    useEffect(() => {
        if (sdpAnswer && sendMessage && camerasInitialized) {
            sendMessage({ type: 'SdpAnswer', data: sdpAnswer })
        }
    }, [sdpAnswer, sendMessage, camerasInitialized])

    const currentDeviceLabel = videoDevices?.[currentDeviceIndex]?.label ?? 'No camera'
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
                {/* <div className="flex items-center gap-2">
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
                </div> */}
            </div>

            <div className={cn(
                "grid grid-cols-2 grid-rows-[auto_1fr_auto] place-items-center gap-4",
                "grow",
            )}>
                <div><b>Nom :</b> {userName || "UNITIALIZED"} <span className="text-sm text-slate-400 ml-2">(ID : {clientId ?? 'No client ID'})</span></div>
                <div><b>Connecté :</b> {rtcConnected ? 'Oui' : 'Non'}</div>
                <div ref={videoContainerRef} className={cn(
                    "relative",
                    "col-span-2 grow h-full w-full",
                    
                )}>
                    {mediaStreamsRef?.current.map((streamData, index) => (
                        <div key={index} className={cn(
                            "absolute inset-0",
                            "flex justify-center items-center",
                            index === currentDeviceIndex ? "visible" : "hidden",
                        )}>
                            <video
                                ref={el => {
                                    videosRefs.current[index] = el
                                }}
                                className={cn(
                                    "w-full h-full object-contain relative -scale-x-100"
                                )}
                                autoPlay playsInline muted
                            />
                        </div>
                    ))}
                    
                    <div
                        className={cn(
                            isFullscreen ? "fixed bottom-2 left-2 z-10" : "absolute bottom-2 left-2",
                            "text-2xl",
                            "bg-black/50 px-2.5 py-1.5 rounded-md",
                            canSwitchCamera ? "cursor-pointer hover:bg-slate-200/50" : "cursor-not-allowed opacity-50",
                            "text-white",
                            "transition-all duration-300",
                        )}
                        onClick={canSwitchCamera ? switchCamera : undefined}
                        title="Switch camera"
                    >
                        🔄
                    </div>
                    <div
                        className={cn(
                            isFullscreen ? "fixed bottom-2 right-2 z-10" : "absolute bottom-2 right-2",
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

                    {isFullscreen && <div className={cn(
                        "fixed top-2 w-full text-center text-2xl font-bold",
                    )}>{currentDeviceLabel}</div>}
                </div>
                <div><b>Nombre de clients :</b> {clientCount}</div>
                {(videoDevices && videoDevices.length) ?
                    <div><b>Caméra ({currentDeviceIndex + 1}/{videoDevices.length}) :</b> {currentDeviceLabel}</div> :
                    <div>Aucune caméra disponible</div>}
            </div>
        </div>
        
        
        
    </div>
};

export default RtcPage;
