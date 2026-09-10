# Little Guy 3000 for Mac

This is a native SwiftUI and AppKit port of the companion's question-and-screen workflow. It runs directly on macOS without Windows, .NET, Wine, or Electron. Voice input uses Apple’s on-device speech recognition; the Mac needs speech resources for its current language. It builds for the current Mac's architecture; the verified build is Apple Silicon on macOS 26.2. The deployment target is macOS 14; Intel and older supported macOS versions still need hardware testing.

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
2. In Settings, enable microphone/local speech and Screen Recording. macOS may require authentication and an app restart. These grants let future questions work without a window chooser. If on-device recognition is unavailable, enable Dictation in macOS Keyboard settings to obtain language support; the app never silently uploads microphone audio.
3. Point at the window you need help with. **Hold Control–Option–Space**, speak, then release. Recording starts after a 300 ms hold; releasing submits the final transcript and a fresh screenshot. Releasing during first-time permission setup cancels the pending recording: finish setup, then hold again. The **Talk** and **Done** buttons provide the same recording flow.
4. **Tap Control–Option–Space** to type instead. Press Send or Command–Return. The screen switch turns automatic context off for voice/text-only questions. The window's app name appears beside it. Little Guy does not listen continuously.
5. The answer streams into the bubble and is spoken when Read answers aloud is enabled. Stop/Escape cancels recording, capture, or the answer. Stop voice silences narration. Full answer opens the larger transcript; its sidebar and model menu can start other modes/conversations.

The pointer selects the topmost ordinary window before Little Guy appears. Follow-ups capture the same selected window afresh; pointing elsewhere and invoking the shortcut selects a new target. If a window closes, permission is missing, or capture fails, the question stays ready to retry and is not silently sent without its requested context. Captures use `SCShareableContent` plus `SCScreenshotManager`, bounded to 2048 pixels, with no audio or cursor. Nothing continuously records the screen.

The larger window retains **+ → Choose a window…**, local image import, and pasted images. Manual window selection uses the macOS sharing picker and its authorized short stream. These remain useful when sharing a specific image. Explain, Walkthrough, and Draft a reply offer textual guidance and copyable drafts. Click a mode or choose a model to leave compact Astra mode and begin that conversation.

The menu-bar icon and floating character reopen the bubble. Settings controls pointer following, reduced motion, and spoken replies. Closing the full window leaves the menu-bar app running. Hide stops the current interaction; Quit stops the app and its helper. New clears the conversation. Reconnecting restores completed text history, without resending old images.

## Privacy and boundaries

- Codex runs as an owned stdio child in `~/Library/Application Support/LittleGuy3000/codex`, with an empty working directory and a separate sign-in. It does not read or overwrite `~/.codex/config.toml` or copy the main Codex app's authentication files.
- The bundle's restrictions are extracted from the existing C# `GuideConfiguration` during the build. The connection disables action tools, uses an empty environment list and capability-root list, requests read-only/never-approve behavior, and refuses server action requests. Synthetic inspection of the actual model request found no exposed tools.
- Questions and screenshots go to the connected cloud model when you press Send or finish a voice question. Quick mode explicitly requests `gpt-6-astra` and `effort: low`; it reports an error rather than substituting a model if unavailable. The model may retain earlier images as conversation context, even after their thumbnail clears. Use New conversation to start without that context.
- The UI keeps its conversation in memory. It does not implement automatic history storage. Codex uses ephemeral threads and disabled history; its own operational/account state can still exist in the separate profile. A synthetic marker scan found no literal test prompt/image output persisted; this is not a comprehensive storage audit.
- Window images are captured directly into memory and bounded to 2048 pixels on the longest edge. Quick mode automatically submits the fresh image with your question; manual attachments show a preview before Send. Little Guy does not write temporary screenshots or print captured content in diagnostic logs.
- This preview does not exclude its windows from screen recordings. Hide Little Guy from its menu when needed.

## Verification

```bash
./scripts/Test-Mac.sh
./scripts/Test-Mac.sh --codex-fixture
./scripts/Test-Mac.sh --voice
```

The first command also rejects incomplete/invalid capture frames and checks sign-in gating, draft/model preservation, image retry, cancellation, conversation recovery, fast completion, and image sizing/PNG encoding. It compiles and exercises the Swift transport against a deterministic stdio fixture: split JSONL writes, Unicode, response limits, stale turn/thread rejection, denied approvals and tools, protocol errors, pending-request cancellation, and reconnect.

The default suite also tests tap/hold/release, final-only transcription submission, Astra Low parameters, fresh follow-up capture, missing permissions/model, cancelled late callbacks, and pointer hit testing on negative monitor coordinates.

The optional `--voice` check verifies that the production spoken-output voice selection generates non-silent audio and completes. It does not record a microphone.

The optional `--codex-fixture` command also launches the real installed Codex runtime against a local synthetic HTTP model fixture. It checks completion, isolated account state, image delivery, and actual tool exposure. It uses no cloud model request or real screen capture. A sandbox must permit binding a loopback port and launching Codex for this check.

`scripts/Build-Mac.sh` also verifies the finished bundle's local code signature. Manual desktop acceptance should cover sign-in, a real streamed answer, screenshot permission/selection/cancellation, image interpretation, follow-ups, copy, speech, menu/hotkey reopen, and quit with no surviving owned helper.

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

The Mac preview does not yet port Windows UI Automation, pointed-control capture, ring/arrow overlays, circle selection, the structured walkthrough verification state machine, automatic reply-field insertion, Spotify/media commands, the multi-card reply pad, protected history/pins, Google Drive research/export, Kokoro voices, or push-to-talk Whisper transcription. Its walkthrough and reply modes are text guidance. Mac push-to-talk uses Apple on-device speech rather than Whisper. The Windows project and its release workflow remain separate.
