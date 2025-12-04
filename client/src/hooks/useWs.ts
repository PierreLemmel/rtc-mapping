import { useEffect, useState, useRef, useMemo, useCallback } from "react"
import { generateId } from "../lib/util";
import type { IncomingMessage, OutgoingMessage } from "../messaging/messages";

const CONNECT_INTERVAL = 1000;

const clientId = generateId();

const getNextDuration = (attempts: number) => {
    switch (attempts) {
        case 0:
            return CONNECT_INTERVAL;
        case 1:
            return CONNECT_INTERVAL * 2;
        case 2:
            return CONNECT_INTERVAL * 4;
        default:
            return CONNECT_INTERVAL * 8;
    }
}

const getWsUrl = () => {
    const wsHost = import.meta.env.VITE_WS_HOST || 'localhost'
    const wsPort = import.meta.env.VITE_WS_PORT || 5174
    const useReverseProxy = import.meta.env.VITE_USE_REVERSE_PROXY === 'true'

    const url = `${ useReverseProxy ? `wss://${wsHost}` : `ws://${wsHost}:${wsPort}` }/ws?clientId=${clientId}`
    return url;
}

export const useWs = (onMessage: (message: IncomingMessage) => void) => {

    const [ws, setWs] = useState<WebSocket|null>(null);
    const socketRef = useRef<WebSocket | null>(null);
    const connectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const attemptsRef = useRef<number>(0);

    const isConnectingRef = useRef(false);
    const [isConnected, setIsConnected] = useState(false);

    const sendMessage = useMemo(() => ws ? ((message: OutgoingMessage) => {
        if (ws) {
            const json = JSON.stringify({
                type: message.type,
                data: typeof message.data === 'string' ? message.data : JSON.stringify(message.data),
                clientId
            })
            ws.send(json);
        }
    }) : null, [ws, clientId]);


    const connect = useCallback(() => {

        if (isConnectingRef.current) {
            return;
        }
        isConnectingRef.current = true;

        if (socketRef.current && socketRef.current.readyState === WebSocket.OPEN) {
            return;
        }

        const url = getWsUrl();
        const socket = new WebSocket(url)
        console.log(`Connecting to WebSocket '${url}' (attempt: ${(attemptsRef.current + 1)})`)

        socketRef.current = socket;
        
        socket.onopen = () => {
            console.log(`Connected to WebSocket '${url}'`)

            attemptsRef.current = 0;
            isConnectingRef.current = false;

            setWs(socket);
            setIsConnected(true);
        }

        socket.onclose = (event) => {
            if (event.reason !== '') {
                console.log(`Disconnected from WebSocket '${url}' with reason ${event.reason}`)
            }
            else {
                console.log(`Disconnected from WebSocket '${url}' with code ${event.code}`)
            }
            socket.close();

            attemptsRef.current++;
            isConnectingRef.current = false;
            connectTimeoutRef.current = setTimeout(connect, getNextDuration(attemptsRef.current));

            setWs(null);
            setIsConnected(false);
        }

        return socket;
    }, [clientId])


    useEffect(() => {
        if (!isConnected) {
            attemptsRef.current = 0;
            connect();
        }
        else {
            if (connectTimeoutRef.current) {
                clearTimeout(connectTimeoutRef.current);
                connectTimeoutRef.current = null;
            }
        }
    }, [isConnected])

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