# WebRTC Stream SDK

Plug-and-play GPU-accelerated WebRTC video streaming for Unity. Wraps
`com.unity.webrtc` and NativeWebSocket signaling behind a simple
`StartStreaming()` / `StopStreaming()` API, with built-in per-player
telemetry and remote control channels.

## Requirements

- Unity 2021.3 or later

## Installation

Add via Package Manager using the git URL:

```
https://github.com/Shravankit/UnityStreamKit.git?path=/Assets/_StreamPackage#1.0.0
```

**Window > Package Manager > + > Add package from git URL**, or add the
line directly to `Packages/manifest.json`:

```json
"com.stream.webrtcstream": "https://github.com/Shravankit/UnityStreamKit.git?path=/Assets/_StreamPackage#1.0.0"
```

### Dependencies

This package depends on `com.unity.webrtc` and NativeWebSocket
(`com.endel.nativewebsocket`). An editor script runs automatically on
first import and installs any missing ones via Package Manager — no
manual setup needed. If it fails (e.g. behind a restrictive network),
add them yourself through Package Manager:

- `com.unity.webrtc` (registry package)
- `https://github.com/endel/NativeWebSocket.git#upm` (git package)

## Installation

### Method 1: Add Directly to `manifest.json` (Recommended)

Open your project's `Packages/manifest.json` file and add the dependencies to the `"dependencies"` block:

````json
{
  "dependencies": {
    "com.endel.nativewebsocket": "[https://github.com/endel/NativeWebSocket.git#upm](https://github.com/endel/NativeWebSocket.git#upm)",
    "com.stream.webrtcstream": "[https://github.com/](https://github.com/)<you>/<repo>.git?path=/Assets/_StreamPackage#1.0.5",
    ...
  }
}

## Quick Start

1. Add a `StreamSDK` component to a GameObject.
2. Create a `StreamConfig` asset (**Assets > Create > WebRTC Stream SDK
   > Stream Config**) and assign it in the inspector.
3. Call `StartStreaming(camera)` on the `StreamSDK` component from your
   own code, or use the sample's `GenericStreamController` to trigger
   it from the inspector/UnityEvents.

```csharp
streamSDK.StartStreaming(myCamera);
````

Subscribe to `OnConnected`, `OnDisconnected`, `OnError`, and
`OnControlReceived` to react to session state and incoming remote
control commands.

## Samples

Import **Examples** from the package's Samples tab in Package Manager
for a working demo scene, plus starting-point scripts
(`GenericStreamController`, `GenericControlReceiver`,
`GenericTelemetryProvider`) meant to be copied and adapted to your
project — they aren't part of the core runtime API.

## License

MIT
