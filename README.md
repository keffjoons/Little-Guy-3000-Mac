# Little Guy 3000 for Mac

Point. Ask. Know.

Native macOS port maintained in [keffjoons/Little-Guy-3000-Mac](https://github.com/keffjoons/Little-Guy-3000-Mac), based on [rafsby/Little-Guy-3000](https://github.com/rafsby/Little-Guy-3000). The original Windows source and history are retained; `upstream` points to the original repository.

## macOS preview

A native SwiftUI and AppKit Mac companion (version **0.6.1**) shows only your latest words and one agent reply beside your cursor, then fades away. A small mascot face appears immediately when you press the shortcut: preparing dots until local input is ready, then a green listening pulse while capturing your held speech. Releasing the shortcut stops the pulse. There are no buttons, status labels or input boxes in the overlay. **Hold Control–Option–Space to talk; release it to mute.** With microphone permission already granted, the app prepares a muted connection ahead of the shortcut and keeps it ready between requests. Early speech is buffered during connection setup, and replies play while the microphone is muted. Hold the shortcut again to interrupt. Codex realtime voice handles the conversation; **GPT-6 Astra with Low reasoning** handles screen questions and native Codex Computer Use. It can operate requested apps in the background while you keep working. With screen context on, each shortcut request automatically captures the selected window, sends the full screenshot to Astra, and supplies visible text to Astra alongside the screenshot. Pressing the shortcut alone stays silent; replies wait for your words. App-specific questions use that context, including questions such as “How do I make a new playlist?” Requested window actions run directly, without an Allow action popup.

On macOS 14 or later, with Xcode Command Line Tools and a current Codex runtime:

```bash
./scripts/Build-Mac.sh
open "artifacts/Little Guy 3000.app"
```

First-time setup needs ChatGPT sign-in, Microphone and Screen Recording permissions for Little Guy, and the Computer Use plugin enabled in Codex. Native controls use Codex Computer Use’s own macOS permissions. **Microphone audio is captured only while you hold the shortcut.** Releasing mutes new input immediately; speech already captured during startup finishes sending through your ChatGPT connection. End voice, Escape, explicit dismissal, sleep, or Quit closes the connection and clears queued audio. Typed questions and manual image attachments remain available. See [the Mac guide](docs/MACOS.md) for tested behavior and supported controls.

## Windows preview

Native Windows 11 companion with a small animated smiley beside your cursor. **Version 0.1.13 is a development preview**, with compact speech bubbles, native UI, capture, local Whisper speech and Codex transport. Production acceptance work remains; see [TESTING.md](TESTING.md).

## Run the preview

In this workspace, double-click `Open Little Guy 3000.cmd` in the project root. It opens the app with the existing development profile. Launching the executable again now reopens the running panel. Normal cold launches show the panel; the development `--connect` launch remains a background start.

Supported direct requests such as **Open Spotify**, **Play Nirvana on Spotify**, and **Open Spotify, Play ADHD Techno** work from the normal Ask window (typed or voice) as well as Ctrl+Alt+Space, when commands are enabled. Ordinary questions and walkthrough objectives stay in explanation mode. Spotify launch-and-play requests accept commas, periods, semicolons, “and”, or “then”. Unmatched app/media requests receive local help instead of an explanation-model refusal. Named playback still depends on Spotify exposing a clear accessible Play target.

On Windows 11 x64, extract the entire portable ZIP and run `LittleGuy3000.Desktop.exe`. Keep its accompanying files together. The package includes .NET and Windows App SDK runtimes; no SDK installation is needed to run it. This preview is unsigned and has not yet been tested on a clean Windows installation.

1. Open **Settings**, choose the installed Codex executable, and select **Connect**. Complete the official browser sign-in. The currently verified Codex version is **0.153.4**; other versions are deliberately rejected until their protocol and capabilities are verified.
2. Enable **screen context** if you want Little Guy to see the selected window. Enable voice separately if wanted.
3. Point at a window and press **Ctrl+Space**. Type a question, then select **Ask** or press **Shift+Enter** (Ctrl+Enter also works). Enter alone adds a new line. An alternate **Ctrl+Shift+Space** shortcut is available in Settings.
4. With voice enabled, hold the shortcut to record and release to transcribe and submit. Whisper large-v3-turbo transcribes English on your PC with no audio upload or transcription charges. Choose your microphone in Settings; the bubble shows input activity, then transcription progress. The first request loads the model. Keep the bundled `Models` folder with the app.

**Ctrl+Alt+H** hides the panel, character and annotations, even when pinned, and stops the active interaction. Ctrl+Space brings Little Guy back. **Ctrl+Alt+Q** quits the app and its owned helper process. **Esc** cancels the current interaction and hides an unpinned panel. Closing the window leaves Little Guy in the tray. The tray menu also offers Hide, Pause and Quit; Settings reports hotkey conflicts.

To create a tutorial, point at a control and press **Ctrl+]**, or press **Ctrl+Shift+]** and drag a box around an interface. Enter what you want to make or learn and press Enter; no keyword is required. Little Guy waits for your goal before generating the first step. For example: “Create a warm pad sound” or “Show me how to use this compressor.”

Follow one instruction, then press **Ctrl+Alt+Enter** or select **Check step**. Little Guy checks a fresh view before moving on, keeping the original app and goal even when your pointer moves elsewhere. Ctrl+] also checks the current step while a tutorial is active. Use End walkthrough before starting a different pointed tutorial, or Ctrl+Shift+] to select a new tutorial area. Selected-area tutorials use the crop for the first instruction and the full target window for later steps, so menus and dialogs are visible. Tutorials guide your actions; they do not automatically build or edit the application for you.

You can also choose **Walkthrough** in the full window and send a goal, or select **Walk me through it** after an answer. Explain and Repeat keep the current step; Back revisits instructions; Skip explicitly skips it. Ctrl+Shift+\ still explains an entire interface immediately.

## Streaming and recording

In **Settings > Streaming and recording**, turn **Show Little Guy in screen recordings** on to demo the app, or off to request capture exclusion. The choice takes effect immediately and is saved. It covers the main panel, smiley, speech/thought bubbles and their tails, highlights, selection outlines and reply drafts. New and older profiles default to hidden until enabled.

For the complete desktop demo, use OBS **Display Capture**. Window Capture of another application or Game Capture does not include Little Guy's separate floating windows. When capture visibility is off, Little Guy remains visible to you; hiding depends on the recorder honoring Windows capture exclusion and is not a universal confidentiality guarantee. This option is separate from allowing Little Guy to see other applications.

## Natural voices and stopping speech

In **Settings > Voice and character > Speaking voice**, choose Heart (default), Bella, Michael, Emma or George. **Preview voice** lets you listen before choosing; Speaking speed applies to subsequent speech. These Kokoro neural voices run locally on the CPU with no speech API charges. The portable app includes the 326 MB model and voice files; the first spoken answer loads the model. Windows default remains available as a classic option.

**Stop speech** appears in the bubble, the Ask panel and voice settings. It silences current and queued narration, including further speech from the same streaming answer. Your answer and walkthrough continue. A new question can speak again; disable **Speak answers aloud** to turn narration off persistently.

## Speech bubbles, circles and voice commands

Speech and thought bubbles sit above Little Guy’s head, with their tails pointing down. Near the top of a monitor, the bubble and character shift down together to stay visible. Busy responses use a shrinking chain of thought bubbles with subtle motion; Reduced motion disables that animation. These display progress and user-facing summaries, never private model reasoning.

- **Ctrl+Alt+C**: draw a freehand circle around an item. Hold **Ctrl+Space** (or your alternate Ask shortcut), ask aloud, then release. The bounding area of the circle is attached; the bubble also accepts typed questions and follow-ups. Enable the microphone in Settings for voice input.
- **Hold Ctrl+Alt+Space**: speak an app/media command, then release. Examples: “Open Spotify”, “Open my Spotify and play”, “Play Yesterday by The Beatles on Spotify”, “Play the playlist Sunday Chill”, “Pause music”, “Next song”, “Previous song”, “Open Calculator”. Spotify, Notepad, Calculator, Explorer and Settings are the supported launch targets.
- **Settings > Circle, speak and control**: disable these explicit commands or try a typed command. Ordinary AI questions do not execute actions. Low-confidence recognized commands are rejected rather than acted on.

Named Spotify playback first opens an exact matching visible saved playlist in Your Library, then falls back to search using Spotify's registered protocol syntax. It invokes a single visible, accessible Play button matching the requested name and checks for its Pause state before reporting playback. An already-playing match is left playing. Ambiguous or unsupported results stay in Spotify for selection. Generic playback uses Windows media-session controls, preferring Spotify and declining ambiguous multi-player targets. ADHD Techno by Saive was successfully played in a live desktop test; other songs, playlists, client languages and the physical microphone still need broader acceptance testing. No Spotify API key is needed for this desktop adapter.

**History** now searches up to 500 recent saved exchanges and can show pinned answers only. **Pin answer** explicitly saves an answer even with automatic history off; deleting history removes pins too. Settings adds speaking-speed control and **Copy diagnostic summary**, which excludes account details, paths, screenshots and conversations.

## Explain an interface · Ctrl+Shift+\

Press **Ctrl+Shift+\**, click and drag a yellow box around an interface, then release. Drag in any direction; **Esc** or right-click cancels. The drag is intercepted so it does not operate controls underneath. Little Guy immediately captures the selection within the application at the box's center and starts an overview. Boxes that extend beyond that application are clipped to its window; neighboring applications are not included.

The bubble gives a summary. **Open full answer** or **Ctrl+Space** shows the complete breakdown, organized by section: visible buttons, knobs, dials, sliders, switches, menus, meters and displays, what they affect, and how they interact. Repeated controls may be grouped; unreadable or uncertain controls are identified rather than invented. Dense interfaces may need a tighter selection or a closer view. Only visible content can be explained; hidden tabs and menus require another selection.

This uses GPT-6 Astra with Low reasoning and a dedicated full-length overview prompt, without the short bubble answer limit. Follow-up questions in the full panel retain the cropped image and use your Medium/High preference. Screen permission and app exclusions apply. No commands or controls are executed. The backslash shortcut currently uses the Windows OEM backslash key; alternate keyboard layouts require testing.

## Reply drafts · Ctrl+Shift+R

Open a post or conversation in the active app. For a single conversation, click its empty reply box and press **Ctrl+Shift+R**. Little Guy drafts a response from the visible context using GPT-6 Astra with Low reasoning. A supported, unchanged focused editor can be filled directly; nothing is submitted. If the editor is unsupported, contains a draft, loses focus or cannot be matched confidently, the reply stays in the sticky pad for copying.

With multiple independent comments visible, the shortcut opens an always-on-top, movable reply pad with up to 12 drafts in screen order. Each card shows the author and comment excerpt, an editable reply and its own **Copy reply** button. Paste the replies where you want them and send them yourself. Scroll the post and invoke again for more comments. A new invocation replaces the pad's previous drafts. Close/Escape closes the pad and discards its displayed drafts; **Clear drafts & close** discards its contents. Ctrl+Alt+H hides it with the rest of the app.

Set your preferred voice under **Settings > Reply drafts > Tone guidelines**: length, formality, language and emoji preferences. The guidelines save automatically. This uses visible screenshots across apps rather than account integrations: X, Facebook, Instagram, Reddit, WhatsApp, Telegram and other readable interfaces can supply context. Live compatibility with each platform/editor is still unverified; automatic insertion specifically requires a writable Windows accessibility ValuePattern. Unsupported rich editors receive copyable drafts.

## Included

### Quick bubbles

With screen context enabled, point at a control and press **Ctrl+[** for an automatic explanation. Little Guy displays a progress bubble, then a streamed short summary. The full panel stays hidden and focus stays in the target app. The bubble and character remain by the original pointer location while you read.

Press **Ctrl+]** for the pointed function and enter your tutorial goal. Use **Ctrl+Alt+Enter** or the bubble's **I've done this** button to check each completed step. Ctrl+] also confirms while a tutorial is active. The reply field accepts follow-up questions; confirming a step is a separate action. No action is executed in another application.

**Ctrl+Space** or **Open full answer** opens the complete answer and conversation, preserving its screen context. Opening the panel does not resend a request that is already running. Later panel questions use Medium reasoning by default; choose High in Settings. Bubble requests explicitly use the connected GPT-6 Astra model with Low reasoning. These choices are checked against the live model catalog. Network and model latency still affect reply time; no fixed response-time guarantee is made.

Progress messages describe app activity, such as looking at the selected control. They do not display the model's private reasoning. The summary is generated before the fuller answer in the same request. A signed-in app starts in the tray, ready for the shortcuts. Bracket shortcuts currently use the Windows bracket virtual keys; non-US keyboard layouts require further testing.

- Original smiley with Idle, Listening, Thinking, Speaking, Guiding, Success, Error and Paused expressions; reduced motion and companion visibility controls.
- Native WinUI panel, tray, global shortcut, typed input, local speech recognition and spoken responses.
- Explicit, on-demand window capture with app exclusions and a visible attached-context label.
- Isolated Codex app-server connection, streamed answers, cancellation, model selection, account status and bounded reconnect attempts.
- Validated ring/arrow annotations, click-through overlay windows, stale-target checks and walkthrough controls.
- Optional Windows-account-protected local text history and deletion.

Guide mode explains and highlights. It does not click, type into other applications or run commands. Screenshot understanding uses the connected cloud model; local speech processing does not make AI answers offline. See [PRIVACY.md](PRIVACY.md).

## Build and verify

Install the .NET SDK pinned in `global.json` and Windows build prerequisites, or use the workspace-local SDK when present. NuGet access is needed for restore.

```powershell
.\scripts\Install-SpeechModel.ps1
.\scripts\Install-NarrationModel.ps1
.\scripts\Build.ps1 -Configuration Release
.\scripts\Test.ps1 -Configuration Release
.\scripts\Test.ps1 -Configuration Release -Speech -CodexFixture
.\scripts\Publish.ps1 -Zip
```

The optional Codex fixture requires Python, the verified Codex executable on PATH and permission to bind a local loopback test server. It sends only synthetic data to its local fixture. Speech and native capture tests must run in a normal interactive Windows session; a restricted automation sandbox can deny these OS services.

For development, build Debug then use `scripts\Run.ps1`. `-SelfTest` generates synthetic character/UI screenshots and a native capability report under `.local\app\self-test`; `-Connect` opens sign-in. Development data stays in `.local\app`. Normal portable launches use `%LOCALAPPDATA%\LittleGuy3000`.

## Product documents

- [Refined product proposal](REFINED_PLAN.md)
- [Architecture](ARCHITECTURE.md)
- [Implementation milestones and acceptance criteria](IMPLEMENTATION_PLAN.md)
