# Little Guy 3000 for Mac

This is a native AppKit port of the companion's question-and-screen workflow. It runs directly on macOS without Windows, .NET, Wine, Electron, or downloaded speech models. It builds for the current Mac's architecture; the verified build is Apple Silicon on macOS 26.2. The deployment target is macOS 14; Intel and older supported macOS versions still need hardware testing.

## Build and open

Requirements: macOS 14+, Xcode Command Line Tools (`xcode-select --install`), Python 3 for the build script, and an installed Codex executable. No Swift package dependencies are downloaded.

```bash
./scripts/Build-Mac.sh
open "artifacts/Little Guy 3000.app"
```

Double-click `Open Little Guy 3000.command` for subsequent launches. The launcher builds only if the app is missing; rerun the build script after source changes. Quit the running app before rebuilding. The bundle is locally ad-hoc signed, not notarized for public distribution. You can move the complete `.app` bundle after building; it contains its guide configuration. Codex remains a separate installed prerequisite.

Codex is discovered at `/opt/homebrew/bin/codex` or `/usr/local/bin/codex`. For the Homebrew/npm installation, Little Guy resolves the packaged native binary so it directly owns the app-server process. Use **Choose Codex…** for another location. The Mac transport was tested against Codex CLI 0.145.0, using its locally generated experimental app-server schema and the synthetic fixture. Unlike the Windows desktop's version gate, the Mac preview negotiates initialization and reads the live model catalog. Other CLI versions require the fixture check below before relying on them.

## Use

1. Open the app. Its connection starts automatically. Choose **Sign in**, finish the official browser flow, and wait for **Connected**. Ask stays disabled until account/read confirms sign-in; the login button then shows Signed in.
2. Type a question, then select **Ask** or press **⌘Return**. Enter alone adds a new line. Subsequent questions retain the conversation.
3. To discuss an interface, select **Attach window…**, choose a window in the macOS sharing picker, select Share Window, and inspect the thumbnail. Use Cancel in the picker to cancel. Capturing alone does not send the image; **Ask** sends the attached image with your question. The attachment clears after submission so a later request does not silently reuse it as current evidence.
4. Choose **Explain interface**, **Walkthrough**, or **Draft reply** for the corresponding prompt. Provide your question or goal. Explain and Draft require a screenshot. For a walkthrough, take a fresh screenshot before asking to check the next step. Replies remain copyable text for your review.
5. Enable **Speak answers** for the installed macOS voice. **Stop speech** silences narration. **Stop** or Escape cancels an answer by terminating only Little Guy's helper; the app reconnects automatically afterward, preserving your question for retry.

**⌃⌥Space** opens the panel. Closing the window leaves the app in the menu bar. Click the floating smiley to reopen it. Enable **Follow pointer** to have it follow while the panel is hidden. The menu offers Hide and Quit. **New conversation** clears the visible answer, question, and attachment and starts a fresh model conversation. Changing mode or model also starts a new conversation.

Screen capture uses SCContentSharingPicker and SCScreenshotManager on macOS 14+. Select the window you want to share each time; the app takes one still image and ends the selection session. No persistent screen-recording grant or Accessibility permission is needed. Capture cancellation and a 90-second selection timeout restore the app controls. Typed questions need no screen access. See [Apple’s screen-sharing picker overview](https://developer.apple.com/videos/play/wwdc2023/10136/).

## Privacy and boundaries

- Codex runs as an owned stdio child in `~/Library/Application Support/LittleGuy3000/codex`, with an empty working directory and a separate sign-in. It does not read or overwrite `~/.codex/config.toml` or copy the main Codex app's authentication files.
- The bundle's restrictions are extracted from the existing C# `GuideConfiguration` during the build. The connection disables action tools, uses an empty environment list and capability-root list, requests read-only/never-approve behavior, and refuses server action requests. Synthetic inspection of the actual model request found no exposed tools.
- Questions and selected screenshots go to the connected cloud model when you press Ask. The model may retain earlier images as conversation context, even after their thumbnail clears. Use New conversation to start without that context.
- The UI keeps its conversation in memory. It does not implement automatic history storage. Codex uses ephemeral threads and disabled history; its own operational/account state can still exist in the separate profile. A synthetic marker scan found no literal test prompt/image output persisted; this is not a comprehensive storage audit.
- Selected-window images are captured directly into memory, bounded to 2048 pixels on the longest edge, and shown as a thumbnail before submission. Little Guy does not write temporary screenshots or print captured content in diagnostic logs.
- This preview does not exclude its windows from screen recordings. Hide Little Guy from its menu when needed.

## Verification

```bash
./scripts/Test-Mac.sh
./scripts/Test-Mac.sh --codex-fixture
```

The first command compiles and exercises the Swift transport against a deterministic stdio fixture: split JSONL writes, Unicode, response limits, stale turn/thread rejection, denied approvals and tools, protocol errors, pending-request cancellation, and reconnect.

The optional second command also launches the real installed Codex runtime against a local synthetic HTTP model fixture. It checks completion, isolated account state, image delivery, and actual tool exposure. It uses no cloud model request or real screen capture. A sandbox must permit binding a loopback port and launching Codex for this check.

`scripts/Build-Mac.sh` also verifies the finished bundle's local code signature. Manual desktop acceptance should cover sign-in, a real streamed answer, screenshot permission/selection/cancellation, image interpretation, follow-ups, copy, speech, menu/hotkey reopen, and quit with no surviving owned helper.

### Verified on September 10, 2026

- Installed bundle: `/Applications/Little Guy 3000.app`; source: `~/Projects/Little-Guy-3000-Mac`.
- ChatGPT Pro sign-in completed through the official browser flow and persisted across app restarts.
- A real request returned the exact expected answer, “Little Guy is connected and working.”
- Stop during a real request restored the question and automatically reconnected. The previous owned app-server PID was no longer running.
- Swift protocol tests and the real Codex synthetic image/tool-isolation fixture passed.
- Native window picker opens, and selection timeout restores the app controls. End-to-end selected-window image interpretation and physical speech/hotkey behavior remain manual acceptance checks; do not treat these as verified from the text-answer test.

## Remaining Windows-only features

The Mac preview does not yet port Windows UI Automation, pointed-control capture, ring/arrow overlays, circle selection, the structured walkthrough verification state machine, automatic reply-field insertion, Spotify/media commands, the multi-card reply pad, protected history/pins, Google Drive research/export, Kokoro voices, or push-to-talk Whisper transcription. Its walkthrough and reply modes are text guidance. Use native macOS text input for questions. The Windows project and its release workflow remain separate.
