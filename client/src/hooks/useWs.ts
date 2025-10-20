import { useEffect, useState } from "react"

export const useWs = (onMessage: (message: any) => void) => {

    const [ws, setWs] = useState<WebSocket|null>(null);
    useEffect(() => {
        if (ws) {
            return;
        }

        const wsHost = import.meta.env.VITE_WS_HOST || 'localhost'
        const wsPort = import.meta.env.VITE_WS_PORT || 5174

        const socket = new WebSocket(`ws://${wsHost}:${wsPort}`)
        
        socket.onopen = () => {
            console.log('Connected to WebSocket')
            setWs(socket);
        }

        socket.onclose = () => {
            console.log('Disconnected from WebSocket')
            setWs(null);
        }
    }, [])

    useEffect(() => {
        
        if (!ws) {
            return;
        }

        ws.onmessage = (event) => {
            const data = event.data;
            const message = JSON.parse(data)
            onMessage(message)
        }
    }, [ws, onMessage])

    return ws;
}