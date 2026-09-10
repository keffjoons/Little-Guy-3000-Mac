# Little Guy 3000 — development preview privacy

## Mac preview

The Mac port uses Apple on-device microphone transcription and Codex cloud voice for spoken replies. When Read answers aloud is enabled, answer text is sent to a separate ephemeral Codex realtime session through the existing Little Guy ChatGPT sign-in. WebRTC receives generated audio and sends a silent audio track to keep the duplex transport active; it does not capture or upload microphone audio. Playback uses an in-memory, nonpersistent WebKit page and stops when dismissed, interrupted, or disabled. The Mac-specific capture and account boundaries are documented in [the Mac guide](docs/MACOS.md#privacy-and-boundaries).

## Windows preview

Screen context and voice start disabled. When screen context is enabled, invoking Little Guy captures the selected application window on demand. Questions and attached window images are sent through your signed-in Codex connection for AI processing. Capture is not continuous, and moving the smiley does not take screenshots. Selected windows can contain private information; this preview does not implement automatic sensitive-field redaction.

Settings includes a process-name exclusion list, initially containing 1Password, KeePass, KeePassXC and Bitwarden. Exclusions are a convenience control, not comprehensive sensitive-content detection. Little Guy does not read your clipboard automatically. A user-triggered Copy action writes the answer to the clipboard.

Speech recognition uses a bundled Whisper large-v3-turbo model through Whisper.net/whisper.cpp, with Vulkan GPU acceleration and a CPU runtime fallback. No audio is uploaded and no paid transcription API is used. Recording is limited to 60 seconds per request and kept in memory; capture stops on release, Cancel, Hide, Pause or lock. Transcription buffers are cleared after processing/cancellation, though OS memory paging and crash dumps are outside this guarantee. Recognized questions are sent through Codex along with any explicitly attached screen context; supported commands are interpreted locally. Settings lets you choose the Windows default microphone or a named input. Playback defaults to bundled Kokoro neural synthesis on the CPU and the default output device. Five natural voices and classic Windows synthesis are selectable. Neural speech makes no runtime network requests and has no API charges. Generated PCM audio stays in memory and is cleared after playback or cancellation. Stop speech silences playback and discards queued narration; an already-running local inference may finish before its output is discarded. The developer model installer downloads public model weights and checks SHA-256; normal recognition makes no network requests.

Optional text history is enabled initially. Question and answer content is encrypted with Windows DPAPI for the current Windows account and stored in SQLite. Timestamps and settings are not encrypted. Other software running as the same Windows user may be able to decrypt this content. Disable history to stop saving future exchanges; Delete local history removes existing entries and starts a new provider conversation. This does not delete provider-side records, OS backups or copies made elsewhere.

Normal launches store app data in `%LOCALAPPDATA%\LittleGuy3000`; the development launcher uses this workspace's `.local\app`. Codex receives a dedicated configuration/home directory. Browser sign-in is managed by Codex, with keyring credential storage configured and no plaintext credential fallback. Little Guy does not import the Codex desktop app's existing credentials or configuration.

The integration requests ephemeral conversations and disables Codex history, analytics and feedback. App logs omit conversation payloads. App screenshot buffers are cleared when discarded; managed strings and the provider's in-memory context cannot be promised immediate secure erasure. Synthetic marker checks have passed, but comprehensive filesystem and crash-retention auditing is still outstanding. Cloud handling remains governed by the connected account and provider's applicable settings and policies.

Pause and session lock cancel the interaction, drop the attached snapshot and stop listening/playback. Guide mode exposes no model tools in the verified runtime fixture, declines execution/permission requests and uses a read-only sandbox. It is not a guarantee that every screenshot instruction or model answer is trustworthy.

The explicit developer `--self-test` mode saves screenshots of its own synthetic UI and detailed test diagnostics. Do not use that mode for private conversations. No general diagnostic export or telemetry service is implemented.

## Reply drafting

Ctrl+Shift+R captures the active application window once, subject to screen permission and app exclusions. The current image, tone guidelines and (when available) the focused empty editor's accessibility label and image rectangle are sent through Codex. Comment text is treated as untrusted data. Each batch uses a new ephemeral thread, without prior posts or conversations. Reply drafts are kept in memory, not saved to local conversation history. Tone guidelines are ordinary unencrypted settings.

For one clearly matched conversation, Little Guy may set the value of the same still-focused, empty accessibility editor. It checks element identity, process, window, bounds, focus, current value and cancellation before insertion. It does not click Send, press Enter, simulate typing, change focus, read the clipboard, open reply boxes or traverse other posts. Editors that cannot be safely filled use the sticky pad. Copy writes the selected edited draft to the clipboard; that copy is then subject to Windows clipboard history/sync and other apps' access. Text inserted into a site's editor may be autosaved by that site even before sending.

Multiple comments always use the sticky pad. A new reply request replaces the previous pad contents. Clear drafts, Delete local history, screen-context disable, Pause/session lock and quitting discard the in-app pad; hiding it retains the in-memory drafts. No model tools have been enabled by this feature. Site-specific compatibility and sensitive-content redaction remain release gates.

## Selected interface overviews

Ctrl+Shift+\ opens an input selector without capturing the desktop. Releasing a valid rectangle captures the application beneath its center, subject to screen permission and process exclusions. The capture is cropped in memory before PNG encoding and AI upload; only the selected intersection with that application is attached. Windows capture temporarily holds the source window frame in memory. The app title remains part of the context. Other windows are not captured as a combined desktop image. Cancel, Hide, Pause, lock and display changes stop an active selection. The selected image is retained in memory for follow-up questions and is discarded through the existing context lifecycle. Optional encrypted text history stores the overview answer, not the image.

## Explicit voice commands and saved answers

Ctrl+Alt+Space listens only while held when microphone access is enabled. Recognized text is processed by a local bounded command parser; commands do not upload screenshots or audio to Codex. The executor can launch five named applications and request media playback actions. Spotify playback inspects its foreground window for an exact visible saved-playlist name in Your Library, opens that match, and invokes a matching accessible Play button. If no saved playlist is available, it passes an encoded search URI using Spotify's registered protocol argument. Ambiguous matches are not clicked. A matching Pause state confirms playback. Spotify handles the search through the user's existing signed-in session. The command toggle, cancellation, Pause and lock stop pending work, but cannot undo an action already completed.

General clicks, arbitrary executables/arguments, shell commands, deleting, purchases and sending messages are outside this executor. Recognizer confidence guards reduce accidental actions but are not a guarantee of transcription correctness. Typed commands are also available. Circle questions use the bounding rectangle of the stroke, subject to the existing single-window capture restrictions.

Pinned answers use the same DPAPI-protected content storage. Pin status and timestamps are plaintext metadata. An explicit pin saves the answer even when automatic history is disabled. Search decrypts up to 500 matching-scope recent records in memory. Delete history also deletes pins. Diagnostic copy writes only version/OS/capability/hotkey status to the clipboard; standard Windows clipboard history/sync behavior applies.

## Streaming visibility

Show Little Guy in screen recordings is off by default. Off requests Windows WDA_EXCLUDEFROMCAPTURE for all application windows; on removes this restriction with WDA_NONE. The setting persists locally and applies to existing and newly created overlays. It does not start recording, broadcast content, or change screen-context permission. When on, answers and reply drafts displayed by Little Guy can appear in your recording. Capture exclusion depends on the recording method and is not a security boundary.
