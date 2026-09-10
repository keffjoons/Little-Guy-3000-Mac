# Little Guy 3000 — proposal for approval

Status: **Approved for implementation by the user, including the animated smiley character.**

Latest scope update: the user authorized ongoing roadmap implementation, genuine speech/thinking bubble shapes, freehand circle-and-ask, and explicit push-to-talk computer control, including Spotify songs and saved/public playlists. This supersedes the earlier deferral of all computer actions. Version 0.1.5 introduces a bounded local app/media command executor; it does not expose general model tools or arbitrary shell execution. Milestone and acceptance status is maintained in IMPLEMENTATION_PLAN.md.

Latest approved interaction: use a compact bubble beside the smiley as the primary surface. Ctrl+[ explains the pointed control immediately; Ctrl+] starts or checks a walkthrough step. The bubble shows app progress, then a concise summary, with an optional goal/reply field for guidance. Ctrl+Space opens the full explanation and conversation. Quick requests use GPT-6 Astra Low reasoning; full-panel follow-ups use Medium or user-selected High. The character/bubble anchor stays still while reading. Model catalog validation is required for reasoning settings, and the original capture/exclusion rules apply to these shortcuts.

Prepared September 9, 2026. The user subsequently authorized immediate development. This plan supersedes conflicting requirements in the original brief. Material changes to privacy promises, recurring costs, or the chosen backend return for a decision.

## Product promise

**Point. Ask. Know.**

Little Guy 3000 is a small, capable Windows companion that explains what the user is looking at, points to relevant controls, and teaches workflows one step at a time. The first release succeeds when a user can ask “What does this do?”, follow with “Show me where I change it,” and then complete “Walk me through it” using spoken or typed interaction.

The first release observes, explains, teaches, speaks, and points. Computer control comes after that experience is reliable.

## Proposed defaults

These are recommendations for approval, not preferences already supplied by the user.

| Decision | Proposed default |
|---|---|
| Initial audience | Personal use first, with architecture suitable for a later private/public release |
| Quality priorities | Ableton Live and Unity when available, plus Windows Settings, Explorer, Edge, VS Code, Notepad, and Calculator |
| Platform | Windows 11 x64 on supported Windows releases; exact minimum build fixed after the speech and packaging tests |
| Capture target | The permitted application window under the pointer; foreground-window mode remains available |
| Capture boundary | Exact target window. Extra surrounding desktop context requires an explicit scope change |
| Activation | Keep Ctrl+Space as the preferred shortcut; offer Ctrl+Shift+Space when conflicts make it unsuitable |
| Voice | Push-to-talk with a local speech path that requires no separate paid API; evaluate Codex realtime as an optional alternative |
| Companion | A subtle original companion near the pointer while enabled; it sleeps when idle and can be hidden or shown only on activation |
| Response panel | Opens near the activation point, then stays still; draggable, keyboard accessible, and constrained to the monitor work area |
| Capture/history | Explicit captures only; local text history on and easy to disable; no intentional screenshot or audio persistence |
| Clipboard/startup/control | Clipboard context off, startup off, computer control off |

“Window under the pointer” means an eligible application window, not Little Guy's own UI. Menus, tooltips, owned dialogs, the desktop, and overlapping windows require explicit target-resolution rules. If the target cannot be resolved safely, offer a text-only question or scope selection; do not silently capture a different application. Show the selected application in the panel.

## The interaction we will build

1. The shortcut freezes the target window, pointer location, and capture policy before the assistant can take focus.
2. Little Guy checks application exclusions, captures the allowed window, and opens its panel approximately 24–40 logical pixels from the pointer.
3. The user types a question or holds the push-to-talk shortcut. The first-run microphone setup makes this behavior explicit.
4. On submission, Little Guy sends the permitted image and bounded context to Codex. The panel identifies the application and indicates when screen context is attached.
5. The answer streams into the panel. Optional speech starts at complete sentence boundaries. The image reflects the activation time; a fresh activation or explicit Refresh is required when the user wants an updated view.
6. “Show me” requests a validated visual target. An uncertain target produces a clarification or general direction, not a precise-looking guess.
7. “Walk me through it” begins an interactive session. Each “Done” takes a fresh permitted observation, checks the result, and produces the next step.

While speech is playing, activation immediately stops playback and queued speech, supersedes the prior answer, and starts a new question. Voice failure always leaves text available. Escape cancels the active operation, stops speech, clears annotations, and dismisses the unpinned panel. A simple panel pin keeps the current answer visible; saved pin collections come later. Pause and Windows lock cancel pending captures and unsent uploads, stop listening/speech, and clear annotations; resume requires user activation. Already transmitted data cannot be recalled. Quit ends owned processes.

## First release scope

| Include | Defer |
|---|---|
| Tray app, hotkeys, near-pointer companion, stable response panel | Double-tap shortcuts and global hands-free conversation |
| Window capture, cursor context, application identification, exclusions | All-monitor capture, region selection, automatic screen-change monitoring |
| Codex sign-in, account status, available usage information, streaming, cancellation | Automatic model routing and additional AI providers |
| Typed questions, push-to-talk, spoken answers, interruption | Dictation into other applications and advanced voice controls |
| Ring/arrow overlays, labels, coordinate validation, basic nearby UI Automation grounding | Elaborate annotation shapes and application-specific adapters |
| Walkthroughs of at least 15 user-confirmed steps, with fresh visual verification | Automatic completion detection |
| Balanced, Quick Answer, Teach Me, Troubleshoot, and walkthrough behavior | Extra overlapping response modes |
| Local session/history controls, copy answer, simple panel pin, onboarding, accessibility | History search, saved pinned answer collections, file drops, skills, plugins |
| Installable build, recovery behavior, diagnostic export, documented compatibility | Computer control, including “Fix it” actions |

Application exclusions and step verification move into the first release. They are prerequisites for the core experience, even though parts of the original roadmap placed them later. All deferred features remain roadmap items rather than unfinished first-release functionality.

## Product identity

Use **Little Guy 3000** for the product, **Little Guy** in conversational copy, and `LittleGuy3000` for internal identifiers. Keep the requested solution and module names. Centralize display names, nickname, tagline, copy resources, icons, and theme tokens.

The visual direction is a premium Windows utility with near-black surfaces, restrained translucency, crisp type, subtle Y2K instrument-panel details, and one restrained accent color. The user-selected companion is an original cute smiley character with rounded eyes and an expressive mouth. It accompanies the pointer without replacing the system cursor. Idle: blink and gentle bob; listening: attentive eyes and an audio pulse; thinking: upward glance and orbiting dots; speaking: moving mouth; guiding: glance toward the target and a small directional gesture; success: happy bounce; error: concerned expression; paused: sleepy face. Reduced-motion mode uses distinct static expressions. Functional errors, permissions, and account messages remain professional.

Stable package identity and storage identifiers are deliberately separate from changeable display branding; changing them later can require migration. The product and assets must be original.

## Decisions that must survive technical validation

The preferred stack remains C#, .NET, WinUI, Windows Graphics Capture, and Codex app-server. Current official documentation describes app-server as an integration surface but also labels the command experimental and unsupported for production workloads. A polished desktop client therefore cannot imply a supported or immutable backend contract. We will pin a tested version, isolate its adapter, and assess public-release suitability at the release gate. [Official app-server documentation](https://learn.chatgpt.com/docs/app-server)

The installed `codex-cli 0.153.4` protocol was inspected without authenticating, capturing the screen, or making model requests. It contains image inputs, ephemeral threads, structured output, and experimental realtime audio methods. Presence in a schema does not prove account access, speech quality, retention behavior, or production readiness.

Milestone 0 must prove four things before substantial feature work: authenticated image questions work; guide-only restrictions are enforceable; screenshots are not intentionally persisted anywhere in the local pipeline; and an acceptable speech path works on the reference machine. If one fails, finish unaffected investigation and present a concrete alternative. Do not silently require API billing, weaken privacy, or enable broader computer access.

Capture and overlays are supported on a declared compatibility matrix. Protected content, the secure desktop, elevated applications, and exclusive fullscreen have limitations; unsupported cases should fail clearly while allowing general text questions. “Useful across Windows applications” is the design goal, not a promise to bypass Windows protections.

## Approval and next step

The open preferences are the initial audience, priority applications/workflows, and voice tradeoff. The table above supplies defaults if no changes are requested.

The user has approved the build with the smiley character change. Proceed through [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md), using [ARCHITECTURE.md](ARCHITECTURE.md) as the technical contract. Record actual validation results separately from intended behavior.
