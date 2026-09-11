# Little Guy 3000 for Mac

This is a native SwiftUI and AppKit port of the companion's question-and-screen workflow. It runs directly on macOS without Windows, .NET, Wine, or Electron. Live voice uses a persistent Codex realtime WebRTC session with microphone input, streamed audio output and interruption. It builds for the current Mac's architecture; the verified build is Apple Silicon on macOS 26.2. The deployment target is macOS 14; Intel and older supported macOS versions still need hardware testing.

## Build and open

Requirements: macOS 14+, Xcode Command Line Tools (`xcode-select --install`), Python 3 for the build script, and an installed Codex executable. No Swift package dependencies are downloaded.

```bash
./scripts/Build-Mac.sh
open "artifacts/Little Guy 3000.app"
```

Double-click `Open Little Guy 3000.command` for subsequent launches. The launcher builds only if the app is missing; rerun the build script after source changes. Quit the running app before rebuilding. The bundle uses a persistent local signing certificate, not Developer ID notarization for public distribution. You can move the complete `.app` bundle after building; it contains its guide configuration. Codex remains a separate installed prerequisite.

Little Guy first uses an explicitly selected Codex executable, then its dedicated runtime, then `/opt/homebrew/bin/codex` or `/usr/local/bin/codex`. The verified dedicated runtime is Codex CLI **0.154.0**; the previously installed 0.145.0 did not advertise Astra. To install the same runtime (requires npm):

```bash
npm install --prefix "$HOME/Library/Application Support/LittleGuy3000/runtime" @openai/codex@0.154.0 --no-audit --no-fund
```

The app resolves npm's native executable and directly owns its helper process. This installation does not change the global Codex CLI. Other CLI versions need protocol and model checks before relying on them.

## Stable local signing

`Build-Mac.sh` calls `Sign-Mac.py`, which creates one private signing identity on first use and reuses it on later builds and checkouts. Its dedicated keychain, password and public certificate live under `~/Library/Application Support/LittleGuy3000/signing`, outside Git. Temporary exported key material is removed after import; the imported key is non-extractable. The keychain is locked after signing and the user's keychain search list is restored. No system or TLS trust settings are changed. Preserve this directory when moving or cleaning up the project; an incomplete identity fails the build instead of silently generating a replacement.

macOS tracks signed updates using the app's designated requirement ([Apple code signing guide](https://developer.apple.com/library/archive/technotes/tn2206/)). Little Guy's requirement binds its bundle identifier to the persistent signing certificate. This replaces the changing binary-hash identity used by ad-hoc builds. Existing grants from those older builds must migrate once. macOS may still request permission after a manual reset, a changed bundle identifier/certificate, or an OS policy change.

Run `python3 scripts/Verify-Mac-Signing.py` after a build to verify changed bundle contents retain the same identity and satisfy the previous build's requirement. This tests signature continuity; it does not bypass or edit macOS permission databases.

## Use

1. Open Little Guy. It stays out of the way until there is a message. Use the menu-bar icon → Settings to sign in or manage permissions.
2. Enable Microphone and Screen Recording for Little Guy. Enable the Computer Use plugin in Codex and grant its native helper the macOS permissions it requests. Little Guy no longer needs its own Accessibility grant for live actions. The app does not listen on launch.
3. Point at the window you need help with. Hold **Control–Option–Space** to speak; release it to mute immediately. When microphone permission is already granted, the app prepares a muted voice connection on launch and wake. Shortcut press enables input immediately, without the old 300 ms delay. Hold it again to speak or interrupt a reply; the connection remains open between requests. If connection setup is still running, held speech is buffered locally and sent in order when ready, even if you have released the key.
4. Releasing the shortcut disables microphone transmission; Escape closes the call and cancels pending actions. Explicit dismissal, opening the full window, display/computer sleep, and Quit also end the call. Automatic fading keeps the muted connection ready.
5. With screen context on, screen questions and action requests are delegated to Astra Low with native Codex Computer Use. It can inspect and operate requested apps in the background, including apps other than the pointed window. Ordinary app-access requests are resolved from the current spoken/typed request without another popup. Native policy restrictions and sensitive-action checks remain in force. Inspecting the result is required before claiming success.
6. The overlay shows your latest words, one agent reply and the mascot face. The face appears immediately on shortcut press, with preparing dots until local input is ready and a green pulse while capturing held speech. Releasing the shortcut stops the pulse. It fades six seconds after speech/activity ends, stays hidden when empty unless the shortcut is held, and lets clicks pass through. Pressing the shortcut starts a new voice request immediately; keep holding while speaking. Use the menu-bar icon → Type a question for the full text composer.

The pointer selects the topmost ordinary window before Little Guy appears. Live screen inspections and typed follow-ups capture the same selected window afresh; pointing elsewhere and invoking the shortcut selects a new target. If a window closes, permission is missing, or capture fails, the question stays ready to retry and is not silently sent without its requested context. Captures use `SCShareableContent` plus `SCScreenshotManager`, bounded to 2048 pixels, with no audio or cursor. Nothing continuously records the screen.

The larger window retains **+ → Choose a window…**, local image import, and pasted images. Manual window selection uses the macOS sharing picker and its authorized short stream. These remain useful when sharing a specific image. Explain, Walkthrough, and Draft a reply offer textual guidance and copyable drafts. Click a mode or choose a model to leave compact Astra mode and begin that conversation.

The menu-bar icon provides Settings and the full composer. The small mascot accompanies the exchange; there is no popup toolbar. Settings controls reduced motion and spoken replies. Closing the full window leaves the menu-bar app running. Hide stops the current interaction; Quit stops the app and its helper. New clears the conversation. Reconnecting restores completed text history, without resending old images.

## Privacy and boundaries

- Codex runs as an owned stdio child in `~/Library/Application Support/LittleGuy3000/codex`, with an empty working directory and a separate sign-in. It does not read or overwrite `~/.codex/config.toml` or copy the main Codex app's authentication files.
- Ordinary typed guide sessions expose no tools. Live sessions enable the code-mode dispatcher and load the installed Codex `unified-computer-use` MCP runtime, discovered from the newest valid local plugin manifest at connection time. No native helper binaries or desktop credentials are bundled or copied. The native MCP server is required; a missing installation produces a visible error. The live thread exposes native tools instead of the old custom Accessibility actions. Shell and filesystem execution environments remain disabled. The client resolves ordinary native app access only for the active thread after actual user words, with screen context enabled. Login, verification, unrelated MCP requests, and OS permission changes are not auto-approved. Completed backing turns release native computer control.
- Typed questions and screenshots go to the connected cloud model when you press Send. Only held microphone samples are captured. During connection setup, at most 30 seconds are buffered in memory, never written to a recording file; queued held speech can finish sending after release, while new input is muted. Ending the call clears that buffer. Screen images are captured on shortcut requests and when native tools refresh the view. Quick mode explicitly requests `gpt-6-astra` and `effort: low`; it reports an error rather than substituting a model if unavailable. The model may retain earlier images as conversation context, even after their thumbnail clears. Use New conversation to start without that context.
- The UI keeps its conversation in memory. It does not implement automatic history storage. Codex uses ephemeral threads and disabled history; its own operational/account state can still exist in the separate profile. A synthetic marker scan found no literal test prompt/image output persisted; this is not a comprehensive storage audit.
- Live voice uses Codex app-server realtime WebRTC v3, verified with runtime 0.154.0 and the existing ChatGPT sign-in. Holding the shortcut automatically captures the selected window. The full image is appended to the backing Astra thread without starting an extra turn, together with visible text from on-device OCR. The audio model delegates screen questions to that backing thread. Each new request replaces the current capture; screen capture is not continuous. Screenshot updates stay out of the realtime input route. Audio and reply text are gated until user words arrive; a new shortcut hold closes that gate again. App workflow questions use this context and delegate for fresh window inspection before answering; disabled or missing screen context clears previous app assumptions. It handles casual dialogue and delegates screen/action requests to the gpt-6-astra backing thread configured with low reasoning. It keeps the connection open after each response. A nonpersistent WebKit player requests only microphone access, with echo cancellation; it stops tracks and closes WebRTC when the call ends. This experimental interface can change with runtime updates; failures are visible and do not silently switch to text-only narration.
- Window images are captured directly into memory and bounded to 2048 pixels on the longest edge. Quick mode automatically submits the fresh image with your question; manual attachments show a preview before Send. Little Guy does not write temporary screenshots or print captured content in diagnostic logs.
- This preview does not exclude its windows from screen recordings. Hide Little Guy from its menu when needed.

## Verification

```bash
./scripts/Test-Mac.sh
./scripts/Test-Mac.sh --codex-fixture
./scripts/Test-Mac.sh --voice
./scripts/Test-Mac.sh --realtime
./scripts/Test-Mac.sh --cold-voice
```

The first command also rejects incomplete/invalid capture frames and checks sign-in gating, draft/model preservation, image retry, cancellation, conversation recovery, fast completion, and image sizing/PNG encoding. It compiles and exercises the Swift transport against a deterministic stdio fixture: split JSONL writes, Unicode, response limits, stale turn/thread rejection, denied approvals and tools, protocol errors, pending-request cancellation, and reconnect.

The default suite also tests tap/hold/release, final-only transcription submission, Astra Low parameters, fresh follow-up capture, missing permissions/model, cancelled late callbacks, and pointer hit testing on negative monitor coordinates.

The default suite requires Node.js for the production audio-worklet checks: held-only input, ordered startup replay, release before connection, immediate warm input, and explicit overflow handling. Swift tests cover microphone preparation before transport setup and complete multi-segment transcripts.

The default suite also checks Codex voice negotiation, exact text submission, playback completion, cancellation, stale events, disconnects and cleanup after a cancelled session starts late. The optional `--voice` check uses the existing Little Guy sign-in and a temporary window to speak a synthetic sentence through the production WebRTC player. It requires network access and verifies non-silent received audio and completed playback. It does not record a microphone.

The default suite tests persistent session lifetime, mute, stale events, tool scope gating, shortcut release during startup and playback-result policy. The optional `--realtime` check sends only synthesized test speech into WebRTC (no microphone capture): it interrupts an answer with a second request in the same call, then tests real voice-to-Astra delegation and dynamic-tool image delivery using a synthetic screen. It also asks “How do I make a new playlist?” against a synthetic Spotify window, checks that inspection occurs without explicitly naming the app, then switches to a synthetic Notes window in the same call. The follow-up must identify both its heading and blue background from the automatically delivered screenshot; its inspection tool does not return another image. It requires the existing sign-in and internet access.

The optional `--codex-fixture` command also launches the real installed Codex runtime against a local synthetic HTTP model fixture. It checks completion, isolated account state, image delivery, and actual tool exposure. It uses no cloud model request or real screen capture. A sandbox must permit binding a loopback port and launching Codex for this check.

`scripts/Build-Mac.sh` also verifies the finished bundle's local code signature. Manual desktop acceptance should cover sign-in, a real streamed answer, screenshot permission/selection/cancellation, image interpretation, follow-ups, copy, speech, menu/hotkey reopen, and quit with no surviving owned helper.

### Version 0.6.1 — September 11, 2026

- Shortcut press enables live input immediately. With microphone permission already granted, launch and wake prepare the muted connection in advance; hiding the transcript preserves it. The mascot pulses when local capture is ready, including while held audio waits for the network.
- Microphone preparation runs before Codex account, tool and connection setup. A bounded in-memory buffer preserves early held speech and drains it in order after connection, including when the shortcut was released during startup. The redundant ICE-gathering wait was removed. Transcription segments accumulate within one hold so later sentences cannot replace earlier ones.
- `./scripts/Test-Mac.sh --cold-voice` injects synthesized speech before an intentionally delayed six-second connection, releases before connection, and requires the opening words, the end of the request and a non-silent spoken response. It then sends a second request immediately through the same warm connection with the window hidden. This exercises real authenticated WebRTC without recording a physical microphone; it does not measure external speaker output.
- The default regression suite and both cold/warm live checks passed. The updated bundle also satisfies the existing Microphone and Screen Recording code requirements; its persistent signing identity is unchanged.

### Version 0.6.0 — September 11, 2026

- Live voice uses the installed native Codex Computer Use MCP runtime instead of the custom AX control tools. It can operate requested apps in the background. Ordinary native app access uses the current request without an extra popup; switching screen context off closes the native session and cancels its control.
- `./scripts/Test-Mac.sh --native` is an opt-in real-device check. Close Little Guy first and leave Spotify open. It uses synthesized speech (no physical microphone capture), resumes the current Spotify track, verifies playback, pauses it, and verifies the paused state. It also checks an automatic real screenshot, eight seconds of silence before speech, microphone mute after release, and unchanged foreground app. It reuses the app's signing identity in a disposable test bundle.
- The default suite and this native acceptance check passed. The separate native app-access handler accepts only ordinary app-access requests for the active, user-authorized session; stale, silent, disabled, unrelated and verification requests are rejected.
- This integration requires Codex's installed Computer Use runtime. It discovers the newest valid local plugin version at each connection; it does not bundle the helper. The app-managed bridge may change in future Codex releases.

### Version 0.5.5 — September 11, 2026

- Shortcut holds and screen updates no longer enter realtime voice as conversation messages. Audio playback, displayed replies and tool dispatch wait for actual user words; a new hold closes the reply gate again.
- The default regression suite passed. A live synthetic-audio check stayed silent for eight seconds with the shortcut held and screenshot context delivered, then answered the Spotify question and correctly described the blue Notes window and its heading after switching windows. Spoken number words are accepted in the fixture heading check.
- The updated build satisfies the actual saved Screen Recording and Microphone code requirements.

### Version 0.5.4 — September 11, 2026

- Restored automatic window screenshots for shortcut voice requests. The backing Astra thread receives the image without starting an extra turn; realtime voice receives the app and on-device OCR text. The window under the mascot is refreshed too.
- Regression checks cover image delivery before requests, app changes during connection, missing/disabled context, and rejection of late captures after sharing is disabled.
- Live synthetic-audio acceptance passed: a generic playlist question used Spotify window evidence, then a second spoken request in the same call identified a new Notes window's blue background and Cedar Notebook 731 heading. The second inspection tool supplied no image, verifying use of the automatically delivered screenshot. Physical microphone and OS capture permissions are separate from this fixture test.

### Version 0.5.2 — September 11, 2026

- Replaced the popup with a compact transcript: latest user words and one latest agent reply. Removed the character, title, buttons, composer, status labels and switches from this surface. Settings and typing remain available through the menu bar.
- Empty launch is invisible. New messages appear near the selected pointer location; the overlay fades six seconds after activity finishes and does not intercept clicks. Escape explicitly dismisses it; late messages cannot reopen an explicitly dismissed overlay. Automatic fading preserves the muted voice connection.
- Regression tests passed for fade timing, active speech, new-message return, empty content and explicit dismissal. The synthetic native preview displayed only the two text blocks. The installed 0.5.2 executable matches the build and its persistent signing requirement passed update-continuity verification.

### Version 0.5.1 — September 11, 2026

- Builds now reuse a local certificate-bound signing identity. A changed-bundle test verifies the new signature satisfies the previous build's exact designated requirement. Migrating from older ad-hoc builds requires one final permission refresh; subsequent builds preserve that identity.

- Shortcut-controlled microphone: hold to speak, release to mute; the realtime connection remains open. Connect voice starts muted. Delayed microphone authorization cannot reopen input after release.
- Default regression tests and changed-bundle signing verification passed. The installed 0.5.1 bundle matches the build and verifies with the persistent certificate. The one-time macOS permission migration is awaiting authentication in System Settings; end-to-end Little Guy playback remains pending that migration.
- Removed the action approval card and continuation/timeout gate. Requested accessible presses and text entry run directly with existing target, freshness and cancellation checks.
- Observed delayed Spotify search-result navigation and verified the playlist Play control starts playback in a direct desktop check. Spotify resource links now open their exact inspected ID through the desktop app's registered URI handler, with time for the UI to update before subsequent playback inspection.

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

The Mac preview does not yet port Windows UI Automation, pointed-control capture, ring/arrow overlays, circle selection, the structured walkthrough verification state machine, automatic reply-field insertion, the multi-card reply pad, protected history/pins, Google Drive research/export, Kokoro voices, or push-to-talk Whisper transcription. Its walkthrough and reply modes are text guidance. Live voice can inspect and operate exposed Mac accessibility controls; Requested actions run without an extra confirmation. Microphone input and spoken replies use the persistent Codex realtime connection. The Windows project and its release workflow remain separate.
