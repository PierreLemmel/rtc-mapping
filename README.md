# rtc-mapping

This project aims to send Video streams from webcam to video-mapping software through WebRTC calls, no install required from user.

## Features

- WebSocket server for real-time communication
- WebRTC client with automatic reconnection
- Real-time mapping data sharing
- Modern web interface

## Getting Started

### Prerequisites

- Node.js (v16 or higher)
- npm or yarn

### Installation

1. Install client dependencies:
```bash
cd client
npm install
```

2. Install server dependencies:
```bash
cd ../server
npm install
```

### Running the Application

1. Start the WebSocket server (in server directory):
```bash
cd server
npm run dev
```

2. In a separate terminal, start the development server (in client directory):
```bash
cd client
npm run dev
```

3. Open your browser and navigate to `http://localhost:5173`

### Available Scripts

#### Client (in client directory)
- `npm run dev` - Start the Vite development server
- `npm run build` - Build the project for production
- `npm run preview` - Preview the production build
- `npm run lint` - Run ESLint

#### Server (in server directory)
- `npm run dev` - Start the WebSocket server with hot reload
- `npm run build` - Build the server for production
- `npm start` - Start the production server
- `npm run clean` - Clean build directory

## WebSocket Server

The WebSocket server runs on port 3000 and provides:

- Real-time communication between clients
- Message broadcasting to all connected clients
- Health check endpoint at `/health`
- Automatic client management

## Client Features

- Automatic WebSocket connection with reconnection logic
- Real-time status updates
- Test connection functionality
- Mapping data transmission
- Modern, responsive UI