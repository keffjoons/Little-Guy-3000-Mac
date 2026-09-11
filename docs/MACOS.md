# Little Guy 3000 for Mac

This is a native SwiftUI and AppKit port of the companion's question-and-screen workflow. It runs directly on macOS without Windows, .NET, Wine, or Electron. Live voice uses a persistent Codex realtime WebRTC session with microphone input, streamed audio output and interruption. It builds for the current Mac's architecture; the verified build is Apple Silicon on macOS 26.2. The deployment target is macOS 14; Intel and older supported macOS versions still need hardware testing.

## Build and open

Requirements: macOS 14+, Xcode Command Line Tools (`xcode-select --install`), Python 3 for the build script, and an installed Codex executable. No Swift package dependencies are downloaded.

```bash
./scripts/Build-Mac.sh
open "artifacts/Little Guy 3000.app"
```

Double-click `Open Little Guy 3000.command` for subsequent launches. The launcher builds only if the app is missing; rerun the build script after source changes. Quit the running app before rebuilding. The bundle is locally ad-hoc signed, not notarized for public distribution. You can move the complete `.app` bundle after building; it contains its guide configuration. Codex remains a separate installed prerequisite.

Little Guy first uses an explicitly selected Codex executable, then its dedicated runtime, then `/opt/homebrew/bin/codex` or `/usr/local/bin/codex`. The verified dedicated runtime is Codex CLI **0.154.0**; the previously installed 0.145.0 did not advertise Astra. To install the same runtime (requires npm):

```bash
npm install --prefix "$HOME/Library/Application Support/LittleGuy3000/runtime" @openai/codex@0.154.0 --no-audit --no-fund
```

The app resolves npm's native executable and directly owns its helper process. This installation does not change the global Codex CLI. Other CLI versions need protocol and model checks before relying on them.

## Use

1. Open Little Guy. A compact bubble appears near the cursor. If disconnected, open its gear button, sign in, and wait for Connected.
2. Enable Microphone and Screen Recording in Settings, plus Accessibility through **Enable window controls…**. macOS may require authentication and an app restart. The app does not listen on launch.
3. Point at the window you need help with. Click **Live voice** or hold **Control–Option–Space** for 300 ms to start the call. Release the shortcut and continue talking naturally; the connection remains open between turns. You can interrupt while Little Guy speaks. Microphone audio goes through your ChatGPT connection.
4. **Mute** disables microphone transmission; **End voice** or Escape closes the call and cancels pending actions. Dismissal, opening the full window, display/computer sleep, and Quit also end the call.
5. With screen context on, screen questions and action requests are delegated to Astra Low. It can inspect the selected window and operate exposed accessibility controls. “Play this playlist” in Spotify can press the identified playlist control and inspect the result. One matching Play/Pause action is allowed per explicit request. Other presses and text entry show an exact action for approval in the popup. Unsupported controls report a limitation; there is no arbitrary coordinate-click or shell fallback.
6. Tap the shortcut to open the bubble without starting voice. Type and Send for the usual question flow. During a live call, typed messages enter that same conversation. Read answers aloud in Settings controls narration of ordinary typed answers.

The pointer selects the topmost ordinary window before Little Guy appears. Live screen inspections and typed follow-ups capture the same selected window afresh; pointing elsewhere and invoking the shortcut selects a new target. If a window closes, permission is missing, or capture fails, the question stays ready to retry and is not silently sent without its requested context. Captures use `SCShareableContent` plus `SCScreenshotManager`, bounded to 2048 pixels, with no audio or cursor. Nothing continuously records the screen.

The larger window retains **+ → Choose a window…**, local image import, and pasted images. Manual window selection uses the macOS sharing picker and its authorized short stream. These remain useful when sharing a specific image. Explain, Walkthrough, and Draft a reply offer textual guidance and copyable drafts. Click a mode or choose a model to leave compact Astra mode and begin that conversation.

The menu-bar icon and floating character reopen the bubble. Settings controls pointer following, reduced motion, and spoken replies. Closing the full window leaves the menu-bar app running. Hide stops the current interaction; Quit stops the app and its helper. New clears the conversation. Reconnecting restores completed text history, without resending old images.

## Privacy and boundaries

- Codex runs as an owned stdio child in `~/Library/Application Support/LittleGuy3000/codex`, with an empty working directory and a separate sign-in. It does not read or overwrite `~/.codex/config.toml` or copy the main Codex app's authentication files.
- The bundle's restrictions are extracted from the existing C# `GuideConfiguration` during the build. Ordinary typed sessions expose no tools. A live session starts its own owned helper with the same isolated profile, enables the code-mode dispatcher using process-local overrides, and registers inspect_window, press_control and set_text as client-handled window tools. Built-in shell, file, browser and computer tools remain disabled. Empty environment and capability-root lists and read-only/never-approve behavior remain in force. The live client checks thread identity, selected window, fresh control references and required approval before dispatch.
- Typed questions and screenshots go to the connected cloud model when you press Send. Live microphone audio streams during the call; screen images are captured only when the backing assistant requests inspection. Quick mode explicitly requests `gpt-6-astra` and `effort: low`; it reports an error rather than substituting a model if unavailable. The model may retain earlier images as conversation context, even after their thumbnail clears. Use New conversation to start without that context.
- The UI keeps its conversation in memory. It does not implement automatic history storage. Codex uses ephemeral threads and disabled history; its own operational/account state can still exist in the separate profile. A synthetic marker scan found no literal test prompt/image output persisted; this is not a comprehensive storage audit.
- Live voice uses Codex app-server realtime WebRTC v3, verified with runtime 0.154.0 and the existing ChatGPT sign-in. The audio model handles casual dialogue and delegates screen/action requests to the gpt-6-astra backing thread configured with low reasoning. It keeps the connection open after each response. A nonpersistent WebKit player requests only microphone access, with echo cancellation; it stops tracks and closes WebRTC when the call ends. This experimental interface can change with runtime updates; failures are visible and do not silently switch to text-only narration.
- Window images are captured directly into memory and bounded to 2048 pixels on the longest edge. Quick mode automatically submits the fresh image with your question; manual attachments show a preview before Send. Little Guy does not write temporary screenshots or print captured content in diagnostic logs.
- This preview does not exclude its windows from screen recordings. Hide Little Guy from its menu when needed.

## Verification

```bash
./scripts/Test-Mac.sh
./scripts/Test-Mac.sh --codex-fixture
./scripts/Test-Mac.sh --voice
./scripts/Test-Mac.sh --realtime
```

The first command also rejects incomplete/invalid capture frames and checks sign-in gating, draft/model preservation, image retry, cancellation, conversation recovery, fast completion, and image sizing/PNG encoding. It compiles and exercises the Swift transport against a deterministic stdio fixture: split JSONL writes, Unicode, response limits, stale turn/thread rejection, denied approvals and tools, protocol errors, pending-request cancellation, and reconnect.

The default suite also tests tap/hold/release, final-only transcription submission, Astra Low parameters, fresh follow-up capture, missing permissions/model, cancelled late callbacks, and pointer hit testing on negative monitor coordinates.

The default suite also checks Codex voice negotiation, exact text submission, playback completion, cancellation, stale events, disconnects and cleanup after a cancelled session starts late. The optional `--voice` check uses the existing Little Guy sign-in and a temporary window to speak a synthetic sentence through the production WebRTC player. It requires network access and verifies non-silent received audio and completed playback. It does not record a microphone.

The default suite tests persistent session lifetime, mute, stale events, tool scope gating, cancelled approvals and playback-result policy. The optional `--realtime` check sends only synthesized test speech into WebRTC (no microphone capture): it interrupts an answer with a second request in the same call, then tests real voice-to-Astra delegation and dynamic-tool image delivery using a synthetic screen. It requires the existing sign-in and internet access.

The optional `--codex-fixture` command also launches the real installed Codex runtime against a local synthetic HTTP model fixture. It checks completion, isolated account state, image delivery, and actual tool exposure. It uses no cloud model request or real screen capture. A sandbox must permit binding a loopback port and launching Codex for this check.

`scripts/Build-Mac.sh` also verifies the finished bundle's local code signature. Manual desktop acceptance should cover sign-in, a real streamed answer, screenshot permission/selection/cancellation, image interpretation, follow-ups, copy, speech, menu/hotkey reopen, and quit with no surviving owned helper.

### Version 0.5.0 — September 11, 2026

- Persistent live microphone/voice session, streaming transcripts, mute, interruption and explicit End voice. Synthetic live audio input and a spoken interruption passed in one open connection.
- Astra Low receives delegated screen/action requests. The client exposes fresh window inspection, accessibility press and approved text entry with target/revision checks.
- Default regression suite passed. A spoken synthetic screen question delegated to Astra, invoked the real dynamic-tool transport, and produced the correct green-square / MAPLE 472 spoken answer from the supplied image.
- Initial handoff testing caught a disabled code-mode host: Astra could see tool definitions but could not dispatch them. The live helper now enables that dispatcher through process-local CLI overrides while built-in shell/file/environment actions remain disabled.
- Installed bundle 0.5.0 (build 6) matches the built executable and passes local signature verification. Screen Recording and Accessibility were refreshed for this bundle; the popup recognizes Spotify as its selected window.
- Desktop microphone and Spotify action acceptance remain pending. macOS logs identify an old microphone code requirement and a fresh permission prompt for this rebuilt app, despite its existing Settings toggle being on. The user must accept that prompt before the installed microphone and actual Spotify action can be verified. Synthetic audio/handoff tests do not establish this device acceptance.

### Version 0.4.0 — September 11, 2026

- Connected spoken replies to Codex realtime WebRTC v3 through Little Guy's existing ChatGPT sign-in; no separate API key. Astra Low still generates answers, and Apple on-device speech still transcribes microphone input.
- The live production voice check reached Speaking, detected non-silent audio, and completed playback. The default regression suite passed, including voice cancellation and stale-event checks. External speaker audibility was not measured with another microphone.
- Installed bundle 0.4.0 also completed Settings → Preview voice and an Astra Low popup reply reading “Codex voice is connected.” The popup reported Finished speaking · Codex voice. Screen access was refreshed for the rebuilt bundle; test text was cleared and automatic screen context restored afterward.
- Stop speech, new questions, New, dismissal, window changes, and Quit release the player. Answer text remains available if voice fails.

### Version 0.3.0 — September 10, 2026

- Compact native bubble built and visually inspected, including connection, error, and answer states. The installed app returned “Little Guy quick mode works.” and reported completed spoken playback. The app-local shortcut reopened the bubble from the full window without inserting a space; physical global hold/release remains a device acceptance check.
- Dedicated Codex 0.154.0 advertises Astra Low. Real Astra Low recognized the synthetic green square / MAPLE 472 and retained text context across reconnect.
- Protocol, session, image, frame, and quick interaction regression tests passed. The upgraded runtime's local backend fixture confirmed image delivery, empty model tool exposure, and completion.
- September 11 device setup: microphone and Speech Recognition grants were confirmed. Error `kLSRErrorDomain/201` was traced in macOS logs to “Siri and Dictation are disabled.” Enabling Dictation in Keyboard settings removed the startup error; Little Guy stayed in Listening until explicitly stopped. Optional Improve Siri & Dictation audio sharing was declined. A real spoken utterance and its transcription still need user acceptance.
- September 11 automatic capture acceptance: after the user authenticated System Settings, toggling Screen Recording and restarting alone left the grant unusable. Removing only Little Guy's entry, adding `/Applications/Little Guy 3000.app` again, and choosing Quit & Reopen cleared the app's warning. With screen context on, the app-local shortcut selected a dedicated synthetic window. A typed question automatically captured it without a window picker; Astra Low correctly answered green and MAPLE 472, and spoken playback completed. The test conversation and fixture window were cleared afterward.
- The hold/release lifecycle and capture failure/cancellation behavior passed injected regression tests. Physical global shortcut hold/release and real microphone transcription remain separate acceptance checks.
- Ad-hoc signing may require refreshing macOS grants after rebuilding. This is not a notarized public release.

### Version 0.2.1 — September 10, 2026 (historical)


- Installed bundle: `/Applications/Little Guy 3000.app`; source: `~/Projects/Little-Guy-3000-Mac`.
- ChatGPT Pro sign-in completed through the official browser flow and persisted across app restarts.
- A real request returned the exact expected answer, “Little Guy is connected and working.”
- Stop during a real request restored the question and automatically reconnected. The previous owned app-server PID was no longer running.
- Swift protocol, session, and image tests passed. The production session correctly identified a green square and MAPLE 472 in a synthetic image using the real cloud model, then retained the code in a follow-up after reconnecting without resending the image.
- The redesigned desktop UI returned a real answer; switching modes preserved the draft; native Settings controls opened and the motion toggle changed correctly.
- The optional real Codex synthetic image/tool-isolation fixture is also available.

For the opt-in cloud image/follow-up check, quit Little Guy, build it, then run `./scripts/Test-Mac.sh --live`. This uses the app’s existing sign-in and sends two small synthetic requests. It does not test the system screen picker or audio output.
- Native file import: the SwiftUI importer enabled Open for the synthetic PNG and produced the attachment preview. It replaces the AppKit sheet whose Open button stayed disabled.
- Paste image: copying the synthetic image file in Finder, choosing Paste image, and sending a question produced the correct green-square/MAPLE 472 answer in the app.
- Selected-window capture: shared only a dedicated synthetic test window through the macOS picker, received the Selected window preview, and got the correct green-square/MAPLE 472 answer in a real app conversation. ScreenCaptureKit logs confirmed stopCapture after the single frame. No broader screen-recording grant was added.
- Voice: the installed voice generated 75,421 audio frames with nonzero amplitude; the app reported Speaking then Finished speaking. Stop speech cleared playback status. Speaker audibility was not measured with an external microphone.
- Regression checks: protocol, session, image loading, invalid/incomplete capture frames, and voice synthesis passed. Native picker cancellation restored the controls and retained an existing attachment.
- Copy answer placed the exact completed response on the clipboard; pasting it into the composer preserved the text.
- The global shortcut is registered without an error; physical keyboard and other Mac hardware/OS versions remain outside this run.

## Remaining Windows-only features

The Mac preview does not yet port Windows UI Automation, pointed-control capture, ring/arrow overlays, circle selection, the structured walkthrough verification state machine, automatic reply-field insertion, the multi-card reply pad, protected history/pins, Google Drive research/export, Kokoro voices, or push-to-talk Whisper transcription. Its walkthrough and reply modes are text guidance. Live voice can inspect and operate exposed Mac accessibility controls; Spotify Play/Pause is the narrow automatic action path. Microphone input and spoken replies use the persistent Codex realtime connection. The Windows project and its release workflow remain separate.
