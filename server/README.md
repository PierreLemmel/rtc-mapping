# RTC Mapping Server

A WebSocket server for real-time communication in the RTC mapping application.

## Features

- WebSocket server for real-time communication
- Health check endpoint
- Message broadcasting to all connected clients
- Automatic client management
- Graceful shutdown handling
- TypeScript support

## Installation

1. Navigate to the server directory:
```bash
cd server
```

2. Install dependencies:
```bash
npm install
```

## Running the Server

### Development Mode
```bash
npm run dev
```

### Production Mode
```bash
npm run build
npm start
```

## API Endpoints

### Health Check
- **GET** `/health`
- Returns server status, connected clients count, and uptime

## WebSocket Events

### Client to Server
- Any JSON message will be broadcast to all connected clients
- Server responds with acknowledgment

### Server to Client
- `welcome` - Sent when client connects
- `ack` - Acknowledgment of received message
- `error` - Error message for invalid requests

## Configuration

- **Port**: Set via `PORT` environment variable (default: 3000)
- **CORS**: Enabled for all origins in development

## Development

The server uses TypeScript and includes:
- Hot reloading with `tsx watch`
- Source maps for debugging
- Strict type checking
