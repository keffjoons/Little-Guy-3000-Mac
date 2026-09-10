# Little Guy 3000 — verification record

Development preview 0.1.0, Windows 11 x64 reference machine, September 2026.

## 0.1.1 walkthrough and shortcut update

- Twenty-four core checks and the C# transport fixture pass. Added regression coverage for objective preservation, no automatic advancement from Start/Explain/Repeat, missing-observation rejection, explicit Skip, Back history repair, observed completion and rejection of whole-window annotations.
- Native WinUI fixture passes starting from an answer and from the Walkthrough selector, visible controls below a long answer, advancement with a fresh capture, and completion. The screenshot was visually inspected. This fixture uses the real app controller, window capture and local scripted Codex transport.
- Live Codex walkthrough with three generated sample-settings images passes pending → verified → complete, including capture-ID matching at each stage. No real user screen was transmitted for this test.
- Shift+Enter submission is handled before the multiline text box inserts a newline; Ctrl+Enter remains supported. New global shortcuts are Ctrl+Alt+H (hide) and Ctrl+Alt+Q (quit). Registration and native message routing are exercised in the native fixture. Physical key presses and the user's original failing application still need a user retest.
- Warning messages now use an explicit text panel. Walkthrough progress no longer expires merely because the model takes longer than 30 seconds; annotation geometry still has its separate age limit.

## 0.1.2 compact bubble update

- Twenty-seven deterministic checks plus the real C# transport fixture pass. New checks cover streaming a summary before the answer, summary length bounds, and bubble placement within mixed-DPI/negative-origin work areas.
- Native app fixture passes quick explanation without the full panel, no foreground-focus change, preserving answer and capture when opening details, GPT-6 Low for quick requests, Medium/High for panel requests, starting/advancing/completing a walkthrough in the bubble, bracket-hotkey registration and hiding the bubble. The local fake server also checks the actual quick request's model/effort parameters.
- Live model discovery confirms GPT-6 Astra supports low, medium and high for this account. Live tests passed a Low-effort summary and synthetic image response, then a Medium-effort follow-up in the same conversation. First summary text for one synthetic text probe arrived in about 4.3 seconds; this is a single observation, not a latency benchmark or guarantee.
- Bubble screenshot visually inspected. The bubble never renders raw reasoning events; it shows app progress and the model's user-facing summary.
- Remaining targeted checks: physical bracket keys on alternate layouts, accessibility/keyboard navigation inside the bubble, and real multi-monitor hardware. Existing broader release gates remain below.

## Earlier verification

- Release compilation with .NET SDK 10.0.401 and Windows App SDK 2.4.0.
- Eighteen deterministic checks covering coordinate transforms (100–200% scaling and negative origins), invalid/stale annotation rejection, exclusions, cancellation generations, walkthrough state, streamed JSON and screenshot-buffer clearing.
- Real C# transport against a synthetic child process: initialization, restricted ephemeral thread options, account parsing, streamed responses, cancellation, late-event suppression and new conversation.
- User-completed browser sign-in verified against the live provider. A text question returned a correct one-sentence volume-control explanation; a generated image's “Enable sound” label was read correctly; model discovery returned six available models. Only synthetic content was sent during these checks.
- Installed Codex 0.153.4 against a local synthetic HTTP fixture: initialization, isolated unauthenticated account, image input reaches the backend, completed response and an empty model-tool inventory. Supplementary literal payload scan found no retained fixture payloads. This is not a live cloud answer or a complete data-lifetime audit.
- Offline Windows synthesis-to-recognition roundtrip using an in-memory phrase; recognized “Show me how to change this setting” in approximately 316 ms. This does not test a physical microphone or real-world recognition accuracy.
- Native Windows Graphics Capture of Little Guy's own synthetic window, 556 × 803 image pixels, in a normal interactive Windows session.
- Original eight-state character contact sheet and native panel screenshot visually inspected.
- Self-contained portable package launched with DOTNET_ROOT pointed at a nonexistent directory, verifying that it does not depend on the workspace SDK. Its synthetic native test passed screen capture, overlay hit-test passthrough and overlay clearing after window movement. Cross-process physical click testing remains outstanding.
- Fixed and retested a publishing defect where the app's WinUI `.pri` resource index was omitted. Publishing now requires and includes that resource.

Latest native self-test details are in the local `self-test/report.json` generated by the test launch. Hotkey registration can fail if another running instance owns the same shortcut; the normal instance previously registered successfully.

## Remaining release gates

- Broader model/usage handling and quota/network error testing; live text, synthetic image input and model discovery have passed.
- Physical microphone, long dictation, unplug/reconnect, playback interruption and selectable audio devices.
- Real multi-monitor/DPI/HDR/portrait/hotplug testing and a representative annotation accuracy benchmark. Mathematical transforms are tested; actual device layouts are not all covered.
- UI Automation context helper, sensitive-field redaction and related process isolation are not implemented.
- Full file-write/retention tracing across success, cancellation and crashes; current marker scans are supplementary.
- Two complete real workflows, including a 15-step walkthrough, detours and incorrect actions. Only walkthrough state and fresh-observation guards are verified so far.
- Narrator, high contrast, keyboard/text-scaling audit, clean-machine install, ARM64, signed installer/update pipeline and two-hour resource soak.
- General diagnostic export, configurable audio devices, robust activation of an already-running instance and broader Codex-version compatibility.

The portable build is useful for development validation. The production milestones in IMPLEMENTATION_PLAN.md are not all complete.

## 0.1.3 reply drafting

- 30 deterministic checks pass, including current-image batch validation, malformed/oversized drafts, multiple/uncertain no-insert rules and cancellation.
- Eight synthetic external-editor checks pass: focused empty field, direct SetValue, preserving an existing draft, focus-change rejection, mid-generation typing, expired target, moved window and cancellation. The fixture never sends keyboard input or clicks a submit button.
- Real Codex transport fixture and live GPT-6 Low tests pass single/multiple classification, current capture IDs, tone input and switching back to ordinary explanation. Live requests used generated sample images only.
- Native reply-pad flow and screenshot checks exercise editable single/multiple cards, no-focus-steal, hotkey registration and Hide. Windows may deny a synthetic host foreground activation; insertion then uses the copy fallback. Automatic insertion is separately verified in the external-editor fixture.
- Commands: test executable `--reply-fixture`, `--reply-live` (connected account), `--composer-check` (interactive Windows), and desktop `--self-test --reply-check` with LITTLEGUY_FIXTURE_EXE. Run one interactive native test at a time.
- Outstanding: real X/Facebook/Instagram/Reddit/WhatsApp/Telegram editor compatibility, localized/accessibility labels, SPA navigation during generation, physical Ctrl+Shift+R and large/mixed-language threads. No platform-wide automatic-insertion guarantee. Protected or unsupported editors must use the pad.

## 0.1.4 interface overview

- 32 deterministic checks pass, including negative-coordinate drag normalization and rejecting tiny/disjoint/invalid selections.
- Native `--self-test --overview-check` verifies input interception, reverse-drag coordinates, selection teardown, cancellation, crop bounds, encoded PNG dimensions, all sample controls in the answer, full-length output, summary bubble, preserved crop on opening details and hotkey registration. Only project-owned synthetic windows are captured by this fixture.
- Live `--overview-live` with a generated four-control audio-interface image covers Gain, Mix, Bypass and Output meter, explains interactions, and explicitly distinguishes inference and unknown settings. No real user screenshot is used in the live probe.
- Remaining: physical shortcut/drag on the user's actual interfaces, unusual keyboard layouts, very large multi-monitor virtual desktops, real mixed-DPI hardware and dense interfaces with unreadable labels. Automated model coverage on one sample does not establish accuracy for every application.

## 0.1.5 voice and roadmap increment

- 35 deterministic checks include explicit voice targets, rejection of arbitrary execution/communication, and closed-circle geometry.
- Native --self-test --roadmap-check validates speech/thought state, circular input, history-off, explicit pin/search/unpin/delete, local synthesis-to-recognition of a command, registered shortcuts, rejected unsupported actions, cancellation before execution, and Hide cleanup. Test speech stays in memory; no physical microphone or real player is operated by this fixture.
- Spotify named-result playback is an accessibility adapter, not a verified universal integration. Song/private-playlist acceptance depends on user-specified test items and the real desktop UI. Media-session actions and voice confidence need real-device testing.
- Remaining broad production gates are listed in IMPLEMENTATION_PLAN.md. This release does not claim general PC automation, microphone/output device selection, automatic redaction, public signing/updating, or completion of the full roadmap.

The synthetic circle test also passes a spoken question through the real application speech callbacks and AI transport while retaining the crop. Legacy history migration is tested using an old three-column SQLite schema and encrypted fixture content. Existing native bubble/walkthrough regression passes after the shape changes.

## 0.1.6 local Whisper recognition

- Windows dictation replaced by Whisper.net 1.9.1 with whisper.cpp, Vulkan GPU and CPU runtime packages. The bundled model is `ggml-large-v3-turbo-q5_0.bin` (574,041,195 bytes), SHA-256 `394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2`.
- Release build: zero warnings/errors. All 35 core checks and the real C# transport fixture passed.
- `scripts/Test.ps1 -Configuration Release -Speech` exercises the production local speech service. Synthetic speech recognized Spotify, Open Spotify, a named playlist request, a song request and an interface question. An unsupported destructive phrase stayed unsupported. Silence emitted no text; cancellation suppressed late output; a new recording after cancellation worked; a missing selected microphone did not silently select another device.
- RTX 4090 / Vulkan: first synthetic one-word decode including model load about 1.1 seconds; subsequent short utterances about 0.30–0.32 seconds. CPU fallback recognized Open Spotify in about 8.7 seconds including model load. These are local test observations, not latency guarantees or real-microphone accuracy measurements.
- Native `--self-test --roadmap-check` passed all 16 checks with Whisper, including a spoken circle question reaching the answer flow, voice command transcription, shape states, history, registered hotkeys and cancellation. Evidence: `.local/whisper-roadmap-check/self-test/roadmap-report.json`.
- Remaining speech acceptance: the user's microphone, accent, room noise, physical input selection/unplug, maximum-length recordings and representative song names. Recognition currently selects English. Windows synthesis still handles spoken output.
- Historical Windows SAPI figures above describe older releases, not the current recognizer. The command confidence guard uses a model token-probability heuristic; it is not a calibrated probability of correct intent. Saying only Spotify does not authorize an action; say Open Spotify.


## 0.1.7 command routing and activation

- Native `--self-test --command-routing-check` passed all nine checks: both reported user requests reached a synthetic local executor through ordinary Ask with no connected cloud provider; question/disabled-command/walkthrough guards prevented action; unmatched commands showed their transcript; activation reopened the panel and Settings. No real music playback is claimed by this fixture.
- Build succeeded with zero warnings/errors. Evidence: `.local/command-routing-check/self-test/command-routing-report.json`.
- Added a workspace launcher preserving the existing app profile, duplicate-launch activation and visible normal cold launch. `--settings` opens Settings; `--connect` retains the explicit background-launch behavior.


## 0.1.8 compound Spotify commands

- Fixed the reported request `Open Spotify, Play ADHD Techno.` falling through to the explanation provider. Spotify launch-plus-media parsing handles comma/period/semicolon separators, and/then combinations and repeated wake/polite prefixes. Punctuation inside music names is preserved. This does not enable arbitrary command sequences.
- Normal Ask routes imperative app/media requests to local handling, including a clear unmatched-command response. Disabled-command, walkthrough and circle guards remain. Voice-triggered actions from normal Ask now use the same confidence threshold as the dedicated command shortcut.
- Release build: zero warnings/errors. All 37 core checks and the real C# transport fixture passed. Native command-routing test: all 12 checks passed, including the exact screenshot request and an unmatched compound with no cloud fallthrough. Evidence: `.local/compound-command-routing-check/self-test/command-routing-report.json`. The fixture records dispatched commands; it does not claim to verify real Spotify playback.

## 0.1.9 Spotify playlist playback

- Fixed Spotify search activation to use `--protocol-uri=spotify:search:...`, matching the installed client's registered command. Exact visible saved playlists in Your Library are selected before trying search; the full accessible button collection is inspected instead of truncating it before the playlist's controls.
- The executor invokes only one matching visible Play control, checks the matching Pause state before reporting success, and recognizes an already-playing match without toggling it. Foreground, cancellation and stale-element guards remain in place.
- Live desktop test: ordinary Ask with `Open Spotify, Play ADHD Techno.` started the requested playlist. Spotify showed `Pause ADHD Techno` and `Now playing: Following by Saive`. This verifies this installed client and saved playlist, not all Spotify clients or catalog results.
- Command results now hide the welcome panel and scroll into view. Release build passed with zero warnings/errors. All 39 core checks and the real C# transport fixture passed; all 13 native command-routing checks passed, including visible command results without the welcome panel. Native evidence: `.local/spotify-result-ui-check/self-test/command-routing-report.json`.
- Remaining: other songs/playlists, duplicate library names, missing accessibility support, non-English client labels and physical voice input acceptance.

## 0.1.9 walkthrough acceptance recheck

- Packaged app: all eight native full-panel checks and all twelve compact-bubble checks passed. Starting from an answer and the mode selector, visible step controls, advancing/completing, preserving detail context, hotkey registration and hiding all passed. Baseline capture and overlay click-through/movement invalidation passed. Evidence: `.local/walkthrough-019-check/self-test/`.
- Live Codex with three generated settings screens returned pending, verified and complete with matching capture IDs at every stage.
- Live Windows Calculator workflow through the actual Ctrl+] shortcut: Little Guy identified the pointed 7 button and instructed another press. Selecting I've done this without changing Calculator correctly kept step 1 and reported the unchanged display. Clicking 7 changed the display from 7 to 77; Ctrl+] then verified that change and completed the walkthrough. The full answer was visibly rendered and step controls disappeared on completion.
- Limitation found: one full-panel response showed Audio playback isn't available. Written guidance and fresh-screen verification continued successfully. Spoken narration requires separate diagnosis; it is not confirmed by this walkthrough test. Highlight geometry passed the native fixture but was not visually evaluated over Calculator. Longer workflows, other applications and physical multi-monitor layouts remain release gates.

## 0.1.10 streaming visibility

- Release build passed with zero warnings/errors; all 39 core checks and the real C# transport fixture passed.
- Native --self-test --stream-visibility-check passed all 11 checks: older-profile default, toggling Windows capture affinity for every existing window (including main panel, bubble, reply pad and selector), late-created overlays inheriting both states, both choices persisted to disk, actual Windows Graphics Capture of the visible main window, toggling back to excluded, and disposal unregistering windows. Baseline capture, annotation click-through and movement invalidation also passed. Evidence: .local/stream-visibility-check/self-test/.
- OBS itself and other streaming clients have not been exercised. Their Display Capture, Window Capture and Game Capture sources have different scopes; the full companion experience needs desktop capture. Capture exclusion is a Windows request and must not be described as universal protection.

## 0.1.11 blank floating-window regression

- Bubble and reply-pad windows now have bounded initial sizes, start explicitly hidden, and use AppWindow.Show(false) plus AppWindow.MoveAndResize for XAML-aware presentation. Capture affinity is changed only when needed, avoiding redundant compositor changes.
- All 21 native streaming checks pass, including actual rendered pixel inspection for the speech bubble, thinking bubble and populated reply pad, before/after capture toggles. Eight repeated toggle cycles leave unused popups hidden; dismissed popups stay hidden; visible surfaces remain bounded and keep foreground focus unchanged. Captured PNGs were visually inspected. Evidence: .local/stream-rendering-check/self-test/.
- All 12 native compact-bubble/walkthrough regressions pass. All 39 core checks and the transport fixture pass. Release build has zero warnings/errors.
- These checks exercise Windows capture on this computer. An OBS scene and long streaming sessions remain outside this automated verification.

## 0.1.12 tutorial goals and reply-window lifecycle

- Reply Drafts now has no native window until a nonempty batch is ready. Dismissing, hiding or clearing it destroys that window and releases its capture-policy registration. This eliminates the unused startup surface that remained after earlier sizing-only fixes. First creation preserves the foreground application.
- All 22 streaming/rendering checks pass, including no idle reply HWND, actual populated-pad pixels, repeated capture toggles and destruction on dismissal (.local/012-stream-final/self-test/). All 12 tutorial checks pass (.local/012-tutorial-check/self-test/): point/region goal prompt, ordinary text starting the guide, no invented goal, next-hotkey dispatch, fresh capture, target and goal preservation, initial crop, full-window follow-up, completion, cancellation and registered shortcuts. All 13 bubble checks and 39 core checks plus transport pass.
- Ctrl+] now waits for a user goal before model generation; Ctrl+Shift+] selects a tutorial area; Ctrl+Alt+Enter verifies the current step. Existing Ctrl+] confirmation remains supported for active tutorials. Tutorial instructions ask for one grounded action and expected outcome; general success across complex application workflows remains an acceptance task.

## 0.1.13 narration and bubble update

- Release build: no warnings or errors; 40 core checks and the C# Codex transport fixture pass.
- `--self-test --narration-check`: actual output-device playback for Heart, Bella, Michael, Emma and George; cancellation before playback, stopping active/queued audio, suppression of later streaming speech without cancelling the answer, restart on a new request, and persisted voice selection. All 14 checks pass.
- `--self-test --bubble-check`: all 13 compact-answer/tutorial checks pass. Above-head placement also has a core geometry check across monitor edges, negative origins and 100–200% scaling.
- Natural voice model SHA256: `0cfd5e79aab70a3d8c1a57dc639835110ddb32c9f5ff4fdd1f4db202ea43bb05`, verified against the official KokoroSharpBinaries v2.0.0 release.
- Subjective voice preference, uncommon audio devices, and clean-machine acceptance still need broader testing.
