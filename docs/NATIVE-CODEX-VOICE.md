# Native desktop voice integration investigation

Checked September 11, 2026 against ChatGPT desktop 26.903.71938 (8576), bundled Codex 0.153.4, and Little Guy 0.6.2 using its separate Codex 0.154.0 runtime.

## Requested behavior

Keep Little Guy's Control–Option–Space hold-to-talk shortcut, mascot, listening indicator, and latest user/assistant message. Let the existing Codex desktop voice session own microphone input, streamed speech output, conversation, screen context, and computer use.

## Result

The native-session switch is not implemented. The inspected interfaces do not provide a verified external connection covering explicit microphone state, session ownership, and live transcript events. This is an integration blocker, not a missing macOS permission or a measured limit of the native voice model. It does not establish that every possible future integration is impossible.

The current app remains a separate realtime client. Reusing Codex app-server and its installed computer-use tools does not attach Little Guy to desktop voice.

## Evidence

- [Official voice documentation](https://learn.chatgpt.com/docs/features/voice) describes starting voice inside the desktop app and configuring its voice hotkey. It also describes conversational interruption and screen context. It does not document an external mascot/control subscription.
- [Official app-server documentation](https://learn.chatgpt.com/docs/app-server) provides a backend for custom clients. The locally generated 0.154.0 `ThreadRealtimeStartParams` schema accepts WebRTC, WebSocket, or `existingCall` transport. `existingCall` binds a call already created by a client; it does not expose the desktop microphone controller or supply that controller's connection to another application.
- The installed desktop bundle's `.vite/build/main-Bkkz0ENj.js` exposes `realtimeVoice`, `realtimeVoicePresentation`, and `realtimeVoiceHistory` on its app host. Voice `control` supports explicit microphone/output mute and stop. Registration uses an Electron MessagePort from a trusted app view, guarded by `isTrustedIpcEvent` before `createAppHost` and `registerAppView`. This is the native connection found in the installed application, not an external endpoint available to Little Guy.
- The native voice `claim` method registers a caller-provided controller and declines when an existing claim is held. It is not a method for attaching another application's controls to an already-running desktop call.
- `/Applications/ChatGPT.app/Contents/Resources/scripting.sdef` exposes browser/window AppleScript commands, including JavaScript execution in browser tabs. It does not define voice-session, microphone-mute, or transcript commands.
- The available Codex app tools expose ending an active voice call and obtaining screen context during a call. They do not expose the start/mute/transcript interface needed for this integration. Native in-app voice commands include microphone-mute toggles; a toggle is not an acknowledged `setMuted(true)` operation suitable for guaranteed release-to-mute behavior.

## Required integration contract

A usable desktop connection must support starting or attaching to a specifically identified native session in a muted state; setting microphone mute explicitly and confirming it; subscribing to input readiness, speaking state, and user/assistant transcript updates; supplying per-request window context; and cancellation/disconnection that leaves input muted. Little Guy could then retain its existing presentation and shortcut code while removing its WebKit microphone and realtime client.

Driving visible buttons or reading rendered text is not equivalent to that contract: window changes and missed toggles would leave release-to-mute and transcript synchronization unproven. No Codex bundle, trust check, desktop account file, or system permission was modified during this investigation. No native voice call was started, and no end-to-end native attachment is claimed.

## Response latency remains unresolved

The current custom client still routes app/screen questions through Astra and explicitly suppresses delegation acknowledgements. Earlier short synthesized-speech checks do not establish the responsiveness of real Spotify or other computer-use workflows. This investigation does not claim a latency improvement or a new app release.
