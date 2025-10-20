import express from 'express';
import { WebSocketServer } from 'ws';
import { createServer } from 'http';

const app = express();
const server = createServer(app);
const wss = new WebSocketServer({ server });

app.use(express.json());


const clients = new Set<any>();


app.get('/health', (req, res) => {
    res.json({
        status: 'healthy',
        timestamp: new Date().toISOString(),
        connectedClients: clients.size,
        uptime: process.uptime()
    });
});


wss.on('connection', (ws, req) => {
    console.log(`New client connected from ${req.socket.remoteAddress}`);
    clients.add(ws);

    ws.send(JSON.stringify({
        type: 'welcome',
        message: 'Connected to RTC Mapping Server',
        clientId: Math.random().toString(36).substring(2, 9),
        timestamp: new Date().toISOString()
    }));

    ws.on('message', (data) => {
        try {
            const message = JSON.parse(data.toString());

            const broadcastMessage = {
                ...message,
                timestamp: new Date().toISOString(),
                sender: 'server'
            };

            clients.forEach(client => {
                if (client !== ws && client.readyState === 1) {
                client.send(JSON.stringify(broadcastMessage));
                }
            });

            ws.send(JSON.stringify({
                type: 'ack',
                originalMessage: message,
                timestamp: new Date().toISOString()
            }));
        }
        catch (error) {
            console.error('Error parsing message:', error);
            ws.send(JSON.stringify({
                type: 'error',
                message: 'Invalid message format',
                timestamp: new Date().toISOString()
        }));
        }
    });

    ws.on('close', () => {
        console.log('Client disconnected');
        clients.delete(ws);
    });

    ws.on('error', (error) => {
        console.error('WebSocket error:', error);
        clients.delete(ws);
    });
});


const port = 5174;
server.listen(port, () => {
    console.log(`RTC Mapping Server running on port ${port}`);
    console.log(`WebSocket server ready for connections`);
});



process.on('SIGTERM', () => {
    console.log('SIGTERM received, shutting down gracefully');
    server.close(() => {
        console.log('Server closed');
        process.exit(0);
    });
});

process.on('SIGINT', () => {
    console.log('SIGINT received, shutting down gracefully');
    server.close(() => {
        console.log('Server closed');
        process.exit(0);
    });
});
