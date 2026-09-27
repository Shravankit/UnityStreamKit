# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-XX-XX

### Added
- `StreamSDK` component with `StartStreaming` / `StopStreaming` API.
- `StreamConfig` ScriptableObject for signaling server, capture quality,
  and telemetry/control settings.
- Per-player telemetry channel (`IStreamTelemetryProvider`).
- Remote control command channel with `IsRemoteControlEnabled` toggle.
- Automatic dependency installation for `com.unity.webrtc` and
  NativeWebSocket on first import.
- Example scripts and demo scene (`GenericStreamController`,
  `GenericControlReceiver`, `GenericTelemetryProvider`).
