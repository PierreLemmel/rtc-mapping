# rtc-mapping

This project aims to send Video streams from webcam to video-mapping software through WebRTC calls, no install required from user.

## Signaling

Signaling server to ensure WebRTC connections


## Web Client

React based web client for web RTC.


## Rtc adapter

The rtc-adapter is based on
- [SIPSorcery](https://github.com/sipsorcery-org/sipsorcery)
- [SIPSorceryMedia.FFmpeg](https://github.com/sipsorcery-org/SIPSorceryMedia.FFmpeg)
- [SIPSorceryMedia.Encoders](https://github.com/sipsorcery-org/SIPSorceryMedia.Encoders)

It also requires [NDI Tools](https://docs.ndi.video/all/using-ndi/ndi-tools/ndi-tools-for-windows) and wraps [NDI sdk](https://docs.ndi.video/all/developing-with-ndi/sdk/example-code)

This requires [FFmpeg](https://www.ffmpeg.org/)

```winget install "FFmpeg (Shared)" --version 7.0```