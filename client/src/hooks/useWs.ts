import { useEffect, useState, useRef, useMemo, useCallback } from "react"
import { generateId } from "../lib/util";

const CONNECT_INTERVAL = 1000;

export type IncomingMessage = {
    type: string;
    data: string;
}

export type OutgoingMessage = {
    type: string;
    data: any;
}

const clientId = generateId();

export const useWs = (onMessage: (message: IncomingMessage) => void) => {

    const [ws, setWs] = useState<WebSocket|null>(null);
    const socketRef = useRef<WebSocket | null>(null);
    const connectIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

    const [isConnected, setIsConnected] = useState(false);

    const sendMessage = useMemo(() => ws ? ((message: OutgoingMessage) => {
        if (ws) {
            const json = JSON.stringify({
                type: message.type,
                data: typeof message.data === 'string' ? message.data : JSON.stringify(message.data),
                clientId
            })
            console.log(json)
            ws.send(json);
        }
    }) : null, [ws, clientId]);


    useEffect(() => {

        const connect = () => {

            if (socketRef.current && socketRef.current.readyState === WebSocket.OPEN) {
                return;
            }

            const wsHost = import.meta.env.VITE_WS_HOST || 'localhost'
            const wsPort = import.meta.env.VITE_WS_PORT || 5174

            const url = `ws://${wsHost}:${wsPort}/ws?clientId=${clientId}`
            const socket = new WebSocket(url)
            
            socketRef.current = socket;
            
            socket.onopen = () => {
                console.log(`Connected to WebSocket '${url}'`)
                if (connectIntervalRef.current) {
                    clearInterval(connectIntervalRef.current);
                    connectIntervalRef.current = null;
                }

                setWs(socket);
                setIsConnected(true);
            }

            socket.onclose = (event) => {
                console.log(`Disconnected from WebSocket '${url}' with code ${event.code}`)
                setWs(null);
                setIsConnected(false);
            }

            return socket;
        }

        if (!connectIntervalRef.current) {
            connectIntervalRef.current = setInterval(connect, CONNECT_INTERVAL);
        }

        return () => {
            if (socketRef.current) {
                socketRef.current.close();
                socketRef.current = null;
            }
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

    return { clientId, isConnected, sendMessage };
}