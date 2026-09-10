# Little Guy 3000 — implementation plan

Status: **Approved; implementation and feasibility checks in progress.**

Current checkpoint: **0.1.6 development preview: local Whisper large-v3-turbo replaces Windows dictation, with microphone selection, input activity, release-to-transcribe and cancellation.** Prior ask/guidance, shaped bubbles, cropped region overview, circle-and-ask, reply drafts, bounded voice commands, history search/pins, speaking rate and diagnostic copy remain. Live sign-in and synthetic AI/speech/native fixtures are verified. This is not completion of all production gates.

### Current delivery status

| Area | Implemented / remaining |
|---|---|
| Architecture and account | Pinned Codex 0.153.4, isolated account, live image/streaming and restricted-tools fixture verified. Full filesystem/crash retention trace remains. |
| Point, ask, explain | Working native app and bubbles, screen permission, exclusions and cancellation. Broader real-application acceptance matrix remains. |
| Accurate selection | Crop transforms, rectangle and freehand circle selection, stale overlays. Expanded UIA grounding, redaction and physical mixed-DPI checks remain. |
| Spoken interaction | Whisper turbo local push-to-talk, Vulkan/CPU runtimes, microphone selector, input activity, circle questions, command shortcut, synthesis, rate setting, synthetic recognition and cancellation guards. Physical mic calibration/unplug tests and output device selection remain. |
| Walkthroughs | Objective/step state, fresh confirmation, Back/Skip/Explain/Repeat, bubble workflow and 18-step state tests. Two long real-app acceptance workflows remain. |
| Usable release | Native character, real speech/thought shapes, encrypted history search/pins, diagnostic summary, portable runtime. Narrator/high contrast/text scaling, clean-machine installation and signed update channel remain. |
| Controlled actions | Explicit voice/typed commands for five named apps and media controls; exact Spotify library selection, corrected search activation and guarded Play/Pause verification. Live ADHD Techno playback passed. General PC clicking, messaging, purchasing, destructive changes and arbitrary execution are not enabled. Broader song/private/public matching still requires acceptance. |

### Next uncompleted work

1. Finish Spotify account-specific song and saved-playlist acceptance, including ambiguous results and missing accessibility support.
2. Add bounded out-of-process UIA grounding/redaction, then verify target accuracy on a representative labeled benchmark.
3. Physically validate microphone accuracy and unplug handling; add output device selection and additional recognition-language settings based on user feedback.
4. Run keyboard/Narrator/high-contrast and multi-monitor acceptance, long real-app walkthroughs and the retention/resource-soak gates.
5. Complete install/sign/update decisions and clean-machine validation before calling the product production-ready.
6. Continue later roadmap items such as attachments, configurable shortcuts, saved workflows and additional adapters after those foundations. No mocked/scaffolded feature is marked complete.


Follow [REFINED_PLAN.md](REFINED_PLAN.md) and [ARCHITECTURE.md](ARCHITECTURE.md). Each milestone ends with working behavior, appropriate tests, known limitations and a commit-ready change set. Scaffolding, mocks and placeholder methods do not count as completed user features. Calendar estimates should follow the feasibility results rather than precede them.

## Milestone 0 — prove the architecture

After approval, inspect the development environment, choose a reference machine/display layout, and create small disposable validation programs. Recheck official documentation and the repository revision corresponding to the selected Codex binary.

| Validation | Required evidence |
|---|---|
| Codex account and image path | Browser-managed sign-in, account read, permitted model discovery, in-memory synthetic image input, streamed answer and interruption work on Windows |
| Restricted capabilities | Inspect actual available tools; adversarial screen text cannot cause commands, private file reads, browser/computer actions, integrations or permission escalation |
| Data lifetime | Trace file writes by the app, Codex and speech components; inspect resulting files/caches after success, cancellation and forced crash. Synthetic marker searches supplement the audit because transformations can conceal literal markers |
| Windowing and capture | Packaged prototype captures the intended target before panel focus changes; ring/arrow stay mathematically aligned at mixed DPI; clicks pass through annotations |
| Speech | One acceptable no-separate-paid-API recognition/synthesis path works on the reference hardware, including immediate playback interruption |
| Packaging | Installed prototype can launch/stop its owned Codex and UIA helpers and run from a standard user account |

Exit: record decisions, measurements, supported versions and remaining limits. If a foundational requirement fails, do not call the gate complete or hide the failure in a fallback. Present the concrete option and its effects on the product contract; continue independent work where useful.

## Milestone 1 — point, type, understand

Build tray lifetime, single instance, pause/quit, configurable activation, target resolution, exclusion checks, on-demand capture, text input, Codex account setup, streaming answer, cancellation and session context. Include an application label, visible screen-attached state, simple panel pin and text-only mode. Add developer diagnostics with metadata/latency only.

Exit: from an installed build, connect the account and complete “What does this do?” followed by “What should I set it to?” in at least two applications. Blocked applications attach no screenshot or private UIA context. Changing focus to Little Guy never changes the captured target. Network failure, quota exhaustion and an incompatible/missing Codex installation produce usable errors. Closing a turn prevents later output from appearing.

## Milestone 2 — point accurately

Implement exact transforms, per-monitor overlay windows, ring/arrow/label rendering, structured command validation, basic nearby UIA grounding and annotation invalidation. Include deliberately ambiguous and unavailable targets in evaluation.

Exit: representative controls receive correctly aligned annotations at 100%, 125%, 150% and 200% scaling, with negative monitor origins and portrait displays. Malformed commands are rejected. Moving/resizing/switching the target while the model is thinking prevents old coordinates from being rendered. Window movement, display hotplug and session lock clear existing annotations. The target application continues receiving mouse input.

## Milestone 3 — ask aloud

Add the validated speech providers, microphone/output selection, push-to-talk, partial/final transcript handling, sentence-based synthesis, auto-speak setting and interruption. The companion visibly communicates microphone state. Preserve text and permit typing if speech is unavailable.

Exit: hold, ask, release, read/hear the answer; interrupt midsentence and ask a correction. No old speech, text or annotations reappear. Microphone denial, device unplug/replacement, output failure and no-speech input recover without losing the answer or sending unintended audio. No persistent raw recordings are produced.

## Milestone 4 — guide a complete workflow

Implement objective-preserving walkthrough state, one step at a time, fresh observation after confirmation, verification/correction, and Next/Back/Repeat/Explain/Skip/Done/Cancel. Handle focus changes, changed layouts, user detours and context compaction.

Exit: complete two representative workflows and a session containing at least 15 steps. Demonstrate an incorrect user action, an ambiguous completion, a missing target and a window move. The assistant records uncertainty honestly and re-observes before drawing. Back never claims to undo an application action. Restart requires fresh context before guidance resumes.

## Milestone 5 — first usable release

Finish the original companion design, native panel/settings, onboarding, text history and deletion, privacy messaging, accessibility, signed packaging strategy and install/uninstall behavior. Settings show only implemented features. Add sanitized diagnostic export, dependency/version checks and bounded process recovery.

Exit: a clean supported Windows installation completes the core ask → show → walkthrough sequence using both text and voice. Verify Narrator, keyboard-only operation, high contrast, reduced motion and text scaling. Confirm no orphaned helpers, no capture/listening while paused or locked, and no unexpected startup registration. Pause/lock also cancels queued captures and unsent uploads, stops speech, and requires explicit reactivation after resume. Test history-off/deletion across database journals, summaries and active sessions.

Public distribution adds a working signed release/update channel, dependency distribution review, and an explicit decision about the experimental Codex dependency. A personal test build may use a documented development certificate; it must not be described as publicly release-ready.

## Proposed measurement targets

These are acceptance targets to validate, not measured claims. Record hardware, OS/app/backend versions, display layout, sample counts and warm/cold conditions. If measurements require a change, document it before declaring the milestone complete.

| Area | Target |
|---|---|
| Warm activation | Visible panel p95 ≤100 ms across at least 100 activations |
| Warm permitted capture | First usable frame p95 ≤200 ms, reported separately from UIA and image encoding |
| Interruption | Audible playback stops p95 ≤100 ms; queued speech is discarded immediately |
| Geometry | ≤1 physical pixel transform/round-trip error within rounding tolerance; rendering checked separately |
| Idle footprint | Mean CPU <0.5% over a 10-minute idle interval on the reference machine; zero capture sessions and zero microphone input while inactive |
| AI grounding | ≥90% correct targets and ≤2% wrong precise targets on a versioned, consented/synthetic benchmark of at least 50 visible-target cases; abstentions remain in the denominator and are reported separately |
| Unavailable targets | No precise annotation in the dedicated hidden/ambiguous/stale-target test set |
| Guidance freshness | Clear within 100 ms of receiving an invalidation event; expire annotations after 10 seconds unless freshly revalidated; stale replies never draw |
| Stability | Two-hour mixed interaction run without crash, unbounded memory growth or orphaned owned processes |

Record median/p95 cloud first-token latency separately; do not promise a fixed network response time. Measure speech transcription accuracy/latency and memory peaks in Milestone 0, then set budgets against the selected local model and hardware. Model self-reported confidence is not the grounding metric.

## Compatibility and fault suite

Baseline applications: Windows Settings, Explorer, Edge, VS Code, Notepad and Calculator. Priority creative applications: Ableton Live and Unity when installed, with specific versions and repeatable workflows selected before acceptance. Expand to Blender, Unreal, Resolve and others only when tested; label untested compatibility honestly.

Cover mixed-resolution monitors, left/above-primary negative origins, portrait orientation, maximized/borderless windows, monitor reconnect and docking, target under the pointer differing from foreground, detached menus/dialogs, tiny text, and dark/light themes. Include elevated/protected/exclusive-fullscreen cases as expected limitations with clear fallback behavior.

Fault scenarios include Codex killed mid-turn, authentication expiry, account limits, offline/reconnect, repeated backend crashes, denied capture, blocked application, private browser content, microphone/output disconnect, malformed/oversized protocol messages, stale events, sleep/wake and Windows lock/unlock. Synthetic screen text instructing the model to run tools must not gain capabilities.

## Required implementation documentation

Keep these planning files current. As their milestones land, add exact setup/build instructions to `README.md`, protocol compatibility and configuration to `CODEX_INTEGRATION.md`, capture restrictions to `SCREEN_CAPTURE.md`, coordinate conventions to `OVERLAY_COORDINATES.md`, verified data flows to `PRIVACY.md`, and reproducible tests/results to `TESTING.md`.

## Later releases

Release 2 adds hands-free mode, double-tap shortcuts, dictation, region capture, expanded grounding, history search, saved pinned answer collections, attachments and model routing as individually validated features. Release 3 introduces controlled computer actions only after an explicit action/permission design and approval. Release 4 may add application skills, integrations, saved workflows and additional providers. No initial milestone depends on these features.
