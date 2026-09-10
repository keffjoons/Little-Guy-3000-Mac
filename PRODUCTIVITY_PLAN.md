# Little Guy 3000 productivity and companion modes

Status: **Proposed — awaiting user approval.**
Prepared September 10, 2026. This document authorizes no implementation by itself. Implementation, builds, feature tests, and agent assignments begin only after the user approves this plan. The user can pause execution at any time; work resumes only on their instruction.

Revision 2 adds Travel, Shopping, YouTube, and Music modes, mode-specific personality graphics, live-information and playback requirements, and corresponding tester gates. The original six features, full shortcut customization, and approval/pause contract remain in scope.

User-selected first music service: **Spotify**. This selection refines the proposed plan and does not constitute implementation approval.

## Outcome

Add all six brainstormed features and four companion modes to the existing Windows companion, with configurable shortcuts for new and existing features. Deliver a usable, tested Windows build, concise usage instructions, and evidence showing what was verified. Keep the compact bubble as the quick interaction surface, with a shared panel for editing, comparisons, saved items, and each mode's focused workspace.

The six features are Hold that thought, What changed?, Personal workflow recipes, Is there a faster way?, Point-and-transform, and Get me moving. They should help users preserve context, investigate mistakes, repeat successful work, learn faster methods, reuse information, and make progress on a focused objective.

The mode list is Work, Travel, Shopping, YouTube, and Music. Work is the existing general-purpose companion experience, enhanced by the six productivity features. The four additional modes bring dedicated workflows and expressive accessories to the same original smiley. The product should feel like a capable, fun companion throughout the user's computer day.

## Starting point

The repository contains contextual answers, compact explanation/walkthrough bubbles, voice input/output, annotations, local protected text history, and reply drafting. TESTING.md records verification through the 0.1.3 reply-drafting work, with outstanding real-world checks. Planning documents are not evidence that those remaining checks have passed.

Current shortcuts are largely hard-coded in TrayService, MainWindow, the bubble, and the reply pad. Settings only switches the main activation shortcut between two choices. Voice release polling specifically checks Space. These paths must be covered by the shortcut refactor, alongside visible help text and tray labels.

The September 10 revision also finds a research-specific live-search path in CodexProvider. Its existence does not establish that travel inventory, product prices, transcripts, or music controls work. Validate and reuse the appropriate isolated search infrastructure after approval; do not inherit its Google Drive publishing instruction for these modes. Capabilities exposed to this planning conversation are not automatically available inside the distributed Little Guy app.

After approval, establish a baseline against the existing working tree and preserve unrelated changes. Do not reset, clean, or overwrite the current uncommitted project work. Use explicit file ownership for agents, with shared integration files owned by the lead.

## Shared product foundation

- A small feature menu exposes all six actions and a visible mode picker without requiring users to memorize shortcuts. Every action remains accessible with buttons or menus when its shortcut is disabled or unavailable.
- A searchable local library holds saved bookmarks, recipes, shortcut tips, focus sessions, trip plans, shopping comparisons, video notes/watchlists, and music discoveries that the user explicitly saves. Filter by item type, mode, app, and user-assigned project. Search titles and saved text without sending the library to the model.
- Generated records are editable. Users control their names, project labels, next actions, and deletions. App/window metadata can suggest a label; it must not silently decide project identity or combine unrelated projects.
- Quick commands show immediate local feedback, stream useful results where supported, and offer Cancel. Large results open the shared panel without losing the target or starting a duplicate request.
- Persist explicitly saved text using the existing Windows-account protection approach and versioned storage migrations. Keep automatic conversation history separate from intentional saves, with clear settings and deletion controls for each. A Delete all local content control covers both categories and active derived context.
- New features use explicit capture or user-supplied text. Before/after images and region captures stay temporary; they are not added to a saved screenshot library. Clear temporary images on discard, pause, lock, quit, and relevant cancellation. Resuming a screen-dependent action requires a fresh observation.
- Reuse app exclusions, cancellation, target identity, model capability checks, and annotation freshness validation. Add narrowly scoped live research, user-selected provider links, explicit transcript import, and user-requested media controls as described below. General computer control and automatic payment are outside these additions. Existing reply drafting and research work remain regression targets.

## Execution order and capability checks

After approval, first record the baseline and inventory the runtime capabilities, installed media players, available accounts, supported browser context, and synthetic test applications. Test planned integrations inside Little Guy's actual application boundary; a successful tool call in the development assistant is insufficient. Record any required account connection, API key, recurring cost, or distribution dependency before committing to a provider. No new paid service is assumed in this plan.

Then execute phases 1–7 for shortcuts and productivity, phase 8 for the mode framework and graphics, phases 9–12 for the four new modes, and phase 13 for integration and delivery. The independent tester gate applies to every phase. Capability blockers are reported with the specific missing access or decision; they do not silently reduce a promised mode to a decorative button.

## Phase 1 — configurable shortcuts

Build a single command catalog, binding model, and dispatcher used by native hotkey registration and local keyboard handling. Commands have stable IDs, display names, scope, defaults, current bindings, enabled state, and press/hold behavior where relevant.

Settings gains a searchable Keyboard shortcuts page. Each command has a record-keys control, a readable binding, clear/disable, and restore-default. Include restore-all, optional alternative bindings, and an explicit Apply/Cancel interaction. Existing users keep their current main activation choice through migration.

Validate duplicates within overlapping scopes, unsupported combinations, and conflicts reported by Windows. Apply binding changes as a set: support swapping two bindings, release obsolete registrations, and roll back on failure. If an external registration race prevents restoring a binding, show its actual unavailable state and keep menu access working. Never show a failed binding as active. Shortcut recording must not execute commands or start dictation.

Binding changes take effect without restarting. All help, tooltips, tray labels, onboarding, error messages, and shortcut hints derive from the current bindings. No feature keeps a separate hard-coded fallback. Normal operating-system text editing and navigation are outside feature shortcut customization.

Preserve current defaults:

| Command | Default |
|---|---|
| Ask / hold to dictate | Ctrl+Space; preserve Ctrl+Shift+Space for users who selected it |
| Explain pointed control | Ctrl+[ |
| Start/check walkthrough | Ctrl+] |
| Draft replies | Ctrl+Shift+R |
| Hide Little Guy | Ctrl+Alt+H |
| Quit Little Guy | Ctrl+Alt+Q |
| Send panel question | Shift+Enter, with Ctrl+Enter as an alternative |
| Send bubble follow-up | Enter, scoped to its input |
| Cancel/dismiss | Escape, with the existing interaction scope |

Proposed new defaults use one consistent family. These are editable starting choices, subject to registration checks on the user's machine:

| Command | Proposed default |
|---|---|
| Open feature menu / library | Ctrl+Alt+Shift+0 |
| Hold that thought | Ctrl+Alt+Shift+1 |
| What changed? — mark before / compare after | Ctrl+Alt+Shift+2 |
| Open workflow recipes | Ctrl+Alt+Shift+3 |
| Is there a faster way? | Ctrl+Alt+Shift+4 |
| Point-and-transform | Ctrl+Alt+Shift+5 |
| Get me moving | Ctrl+Alt+Shift+6 |
| Open mode picker | Ctrl+Alt+Shift+7 |
| Work mode | Ctrl+Alt+Shift+W |
| Travel mode | Ctrl+Alt+Shift+T |
| Shopping mode | Ctrl+Alt+Shift+S |
| YouTube mode | Ctrl+Alt+Shift+Y |
| Music mode | Ctrl+Alt+Shift+M |

Secondary actions also appear as individually bindable commands, initially unassigned: resume a bookmark, save/run a recipe, reset a comparison, save a tip, copy a transformation, complete a focus step, break down a step, pause/resume a focus session, and app pause/resume. Existing walkthrough actions, settings/library navigation, and other feature keyboard actions use the same catalog. Availability follows the current state and input scope; an unavailable command explains the required next step instead of acting on stale context.

Mode commands use the same catalog: cycle modes; search/refresh results; save a trip; compare shortlisted options; open a provider's booking or product page; summarize a video; save video notes; show related videos; open the watchlist; play/pause; previous/next track; recommend similar music; and save a music discovery. Secondary defaults remain unassigned so entering a mode does not take over the user's existing media or browser keys. Final transaction submission stays on the provider's review/checkout interface and is not a global hotkey.

Voice press/hold/release must follow the configured activation binding rather than Space. Releasing the activation key or a required modifier stops microphone capture; cancellation, rebinding, focus/session loss, and pause clear the hold state. Test the existing tap-versus-hold behavior after remapping.

Acceptance: remap each existing and new primary command; verify old keys stop triggering it, new keys trigger it once, settings survive restart, conflict/disable/reset and binding swaps work, displayed keys match behavior, and remapped dictation releases correctly. Exercise physical input in Windows, plus US and at least one non-US layout where available. Record any unavailable layout coverage explicitly.

## Phase 2 — Hold that thought

Interaction: invoke while working, review a compact draft containing the objective, current position, attempted approaches, blocker, and next step, then save it with a title and optional project. Use the active conversation and an explicitly permitted current capture; text-only entry also works. Missing information stays editable or prompts a short follow-up rather than being invented.

Resume from the library or a bound command. Show the saved recap immediately. The user can continue text-only or attach the current app to refresh guidance. A saved label identifies context but does not imply Little Guy reopened or restored the application.

Example: “Pause the Unity movement fix. We fixed input; next check the collision layer.” Tomorrow, “Where was I?” surfaces that next action.

Acceptance: save two distinct project bookmarks, restart, find the correct one, edit and resume it, refresh against a changed screen, and delete it. Verify protected storage, history-off behavior, interrupted saves, and absence of saved image payloads in application-owned storage. A fresh capture must replace stale screen geometry before guidance draws.

## Phase 3 — What changed?

Interaction: first invocation marks a before checkpoint in the selected permitted window. The next invocation captures an after checkpoint and shows a comparison. Buttons explicitly offer Mark before, Compare now, and Start over; the bubble always identifies the current state and target.

Show visible setting/text/layout differences with before and after values where readable, then a short list of plausible things to investigate. Separate observed differences from possible explanations. Users can ask about a difference or launch a walkthrough to examine it.

Compare checkpoints from the same app/window identity. If the window was replaced, the relevant content is missing, or layout changes make alignment unreliable, explain the limitation and request a new baseline. Use current geometry for any annotation. Discard temporary images when ending the comparison; optionally save an editable text summary.

Example: “My character disappeared after those Inspector edits. Show me which visible values changed.”

Acceptance: known changed values, unchanged screens, scrolling/resizing, multiple simultaneous changes, unreadable fields, wrong-window attempts, and cancellation. On a versioned synthetic set, every deliberately visible changed field must be represented correctly and unchanged controls must not be asserted as changed. Separately run repeatable real-app cases and record actual results. A screenshot difference must not be described as proof of an unseen cause.

## Phase 4 — Personal workflow recipes

Interaction: after a successful walkthrough, choose Save as recipe. Produce an editable title, intended result, app/project tags, prerequisites, ordered steps, and expected checks. Support creating a recipe manually and editing, duplicating, searching, or deleting an existing one.

Retain which original steps were verified, skipped, or uncertain. Do not silently turn an incomplete session into a proven recipe. User-edited and manually written steps are allowed and visibly distinct from verified execution history.

Run a recipe in the existing step-by-step guide. Adapt instructions to fresh observations while preserving the saved objective and important settings. If the interface or prerequisites differ, explain the mismatch and revise the current run. Updating the saved recipe is an explicit action. Reuse Back, Explain, Repeat, Skip, and End with correct state handling.

Example: “Run my game-audio export process,” then follow the saved choices against the current export dialog.

Acceptance: save and rerun a real completed workflow; edit and rerun a copy; handle a changed UI, missing prerequisite, failed step, skipped step, restart/resume, and a 15-step session. Do not advance a visually verifiable step without the existing fresh-observation check. For outcomes that cannot be seen, retain uncertainty and allow an explicit user-reported completion without labeling it visually verified.

## Phase 5 — Is there a faster way?

Interaction: point at a control or describe a repetitive sequence. Return a concise useful alternative: a keyboard shortcut, batch operation, or shorter sequence, with a brief explanation of when it applies. Ask for app/version context when needed instead of presenting a guessed shortcut as established fact.

Offer Show me to start a walkthrough and Save tip to add an editable, app-tagged entry to the local cheat sheet. Allow users to mark a tip useful, incorrect, or outdated, and correct or remove it. The feature runs when invoked; it does not watch the user's behavior in the background.

Example: “I keep changing these one at a time. Is there a faster way?”

Acceptance: test a fixed set of real actions against the supported apps and confirm suggested shortcuts/sequences actually work in the tested versions. Include a case where there is no verified faster method. Verify tip saving, searching, correcting, deletion, and launching a grounded walkthrough. Compare concrete step counts where possible; do not invent time-saved statistics.

## Phase 6 — Point-and-transform

Interaction: invoke on selected text or choose a region within the selected permitted app window. Offer Make clearer, Shorten, Turn into checklist, Extract table, and a custom instruction. Show the source alongside an editable output preview, with Copy and Retry.

For text selection, use bounded read-only accessibility data where supported; provide explicit paste input when it is unavailable. Clipboard context is only read after the user's paste action. For visual input, implement a window-bounded region picker that respects exclusions and capture transforms. It must not expand capture to neighboring windows or the whole monitor. Show what will be attached before submitting the transformation.

Extracted values preserve units, labels, and ordering. Mark unreadable or ambiguous cells instead of guessing. Table output supports copying tab-separated values for spreadsheet use. Copying output does not replace text in another application or submit anything.

Example: select a block of notes, turn it into a checklist, or extract a small visible table for a spreadsheet.

Acceptance: supported and unsupported text selection, pasted input, small-font region text, partial tables, mixed units, multi-line cells, mixed DPI geometry, excluded windows, cancellation, and moving the target during selection. Compare extraction against known source values and paste the table into a real editor/spreadsheet to verify its structure. Test actual editing and copy behavior through the UI.

## Phase 7 — Get me moving

Interaction: enter a goal and available time. Little Guy proposes a small finishable objective and a short action list. After the user accepts or edits it, keep the current action in a compact pinned card. Done advances user-reported task progress; I'm stuck breaks the current action into smaller steps while keeping the original objective.

Include optional elapsed/remaining time, session pause/resume, adjust goal, finish, and save a resume bookmark. Time is a planning aid, not a guarantee. Never infer completion from time passing. Visible inspection, if requested, uses a separate fresh capture and distinguishes visual evidence from user-reported completion.

App pause and Windows lock pause the session and stop active capture/voice; no automatic restart or prompts after unlock. A session with no saved record remains temporary. An explicitly saved session can be resumed after relaunch, with no claim that external app state was restored.

Example: “I have 20 minutes to improve this game menu.” Agree on one specific improvement and keep its next action visible.

Acceptance: create and edit an objective, complete and break down steps, pause/resume the timer, change the time budget, survive sleep/lock without accidental progression, restart a saved session, and finish with an accurate recap. Verify the card stays readable without stealing focus or obstructing essential controls.

## Phase 8 — mode framework and personality graphics

Modes organize workflows, preferences, saved items, and the companion's appearance. Answer styles such as Quick Answer, Teach Me, and Walkthrough remain separate from modes. The selected mode is always named in the bubble/panel and accessible to screen readers.

Users switch through the mode picker, a remappable direct shortcut, or a clear typed/voice request. Opening a website does not silently change the mode. Switching preserves editable drafts and intentional saves, cancels the previous mode's active research turn, and prevents late answers or animations from appearing in the new mode. Shared productivity actions remain available, so a trip or video session can also be bookmarked.

Give each mode a recognizable, original accessory set and a restrained accent color:

| Mode | Personality graphics | Example working animation | Useful status copy |
|---|---|---|---|
| Work | Small pencil and notepad | Jots a note; happy checkmark on a verified save | Saving your place… |
| Travel | Explorer cap, miniature suitcase, folded map | Unfolds the map while researching; small departure flourish after an itinerary is saved | Comparing flights for your dates… |
| Shopping | Deal-hunter visor, magnifying glass, little price tag | Inspects two tags while comparing; brief pleased reaction when results are ready | Checking the total cost… |
| YouTube | Small red play badge and notebook | Takes notes while processing an available transcript; flips to a saved takeaway | Pulling out the main points… |
| Music | Oversized headphones and a few floating notes | Gentle head bob while verified playback is active; headphones stay on when paused | Playing… / Finding similar artists… |

Preserve the existing smiley's proportions and facial identity. Accessories must remain recognizable at actual cursor-companion size, without obscuring the eyes, microphone state, error state, or surrounding UI. Start with a reviewable contact sheet and a small-size render pass after approval, then build reusable original vector accessories/animation layers in the current renderer.

Keep mode and activity separate in the state model: selected mode controls the outfit; actual activity controls Idle, Listening, Thinking, Speaking, Guiding, Success, Error, and Paused expressions. Traveling clothes do not mean a booking is running; headphones do not mean music is playing. Use a limited set of additional activity labels such as Searching, Comparing, Reading transcript, and Playing, driven by real events. Music motion follows playback state; it does not imply microphone listening or beat analysis.

Settings includes previewable outfits, subtle/playful animation intensity, reduced motion, and an accessory visibility toggle. Reduced motion uses distinct static poses. No sound effects by default. Short celebrations cannot delay results or obscure a failure. Users can retain a plain smiley with a text mode badge.

Each mode has a separate active conversation/task context. Saved preferences such as shopping currency or music taste are explicit and editable. Share a specific item between modes only through an intentional action; do not automatically forward unrelated travel details, shopping history, or listening metadata.

Acceptance: inspect all five outfits across the existing eight expressions, actual companion size, light/dark backgrounds, 100–200% scaling, and reduced motion. Exercise mode switching during streaming, dictation, errors, and app pause; verify correct labels, clean cancellation, preserved drafts, and no stale output. Test every mode's remapped shortcut and menu route. Demonstrate that the outfit communicates the mode even in a static screenshot and that the app never animates a completed action before it succeeds.

## Shared live research and provider behavior

Travel and Shopping require current source-backed results. YouTube and Music recommendations need real, identifiable videos/tracks with working source links. Implement a shared result contract with source URL/title, retrieval time, relevant dates, country/currency, item identity, and field-level unknowns. Structured model output is a proposal to validate, not proof that a provider returned an offer.

Verify listings against accessible provider pages or an authorized inventory/catalog adapter. Search snippets can suggest candidates but cannot establish a bookable fare, current stock, total price, or checkout availability. Keep observed values separate from estimates; distinguish taxes included, taxes unknown, delivery cost unknown, and currency conversions. Recheck selected offers before checkout. Never claim the cheapest or best anywhere when only a bounded set was compared.

Use a dedicated mode research session with only the necessary read-only search/fetch capabilities. External pages and transcripts are untrusted content and cannot authorize tool changes, purchases, local-file reads, or cross-mode data access. Validate outgoing links. Opening a selected public/provider link is a user action; do not open a browser tab for every search result.

Run search, comparison, and playback operations on request. Continuous deal/price watching and periodic recommendations are not part of this expansion. On service failure, explain what could not be checked, preserve user input, and provide Retry or a useful source link. A manual link route is clearly distinguished from a verified search or active playback capability.

## Phase 9 — Travel mode

Purpose: help plan and book a trip by finding suitable flights, hotels, and experiences, comparing them against the user's requirements, and carrying the shortlist through to the provider's booking flow.

Collect departure point, destination, dates/flexibility, travelers, budget/currency, and important preferences in an editable trip brief. Ask only for missing fields needed by the current search. Support requests such as a weekend trip, a hotel near an event, or one specific experience. Save an itinerary/shortlist with source links and the date each offer was checked.

Flight comparisons show relevant airports, local departure/arrival dates and time zones, stops, duration, cabin/fare identity, baggage information when available, and total/per-person pricing clearly. Hotels show exact stay dates, room/occupancy, location, total stay price, fees, and cancellation terms when confirmed. Experiences show date/time, duration, meeting location, party size/eligibility, inclusions, cancellation terms, and current availability where supplied. Unknown terms remain unknown.

Booking flow: select an option, refresh its details, review the itinerary and total/known fees, then open the exact provider offer or a criteria-preserving provider search page. Clearly identify when a provider cannot preserve the selection. Little Guy can guide the remaining booking steps against an explicit current screen. The user completes traveler details, login, payment, and final purchase on the provider's site. A saved itinerary or opened checkout is not labeled Booked. Confirmation is either explicitly reported by the user or read from a user-attached confirmation, with that evidence type retained. This initial mode provides assisted booking; unattended reservations and payment submission are separate capabilities.

Example: “Find a three-night Vancouver trip from Edmonton, with a central hotel and a food tour, within my budget.” Little Guy builds a sourced shortlist and guides the chosen bookings.

Acceptance: test date changes, flexible dates, overnight flights, multiple airports, multi-traveler totals, room occupancy, cancellation differences, sold-out experiences, unknown fees, changed fares, and source failures. Verify at least one live flight, hotel, and experience shortlist against provider pages. Complete the selection-to-review/confirmation state flow in a synthetic or sandbox booking fixture, including cancellation and a changed total, without purchasing a real trip. Record which live provider handoffs were tested; do not claim an API booking integration based on browser guidance.

## Phase 10 — Shopping mode

Purpose: find suitable items, identify good current offers, and make comparisons based on the user's criteria.

Collect item/category, budget/currency, delivery country or user-supplied postal area when necessary, must-have features, and preferences such as new/refurbished or specific retailers. Let users supply candidate product links or request discovery. Separate hard requirements from preferences; an item that misses a hard requirement is visibly excluded or labeled as an alternative.

Show a comparison table with exact model/variant/condition, relevant specifications, seller, stock information, item price, shipping/tax when known, total or explicitly incomplete total, return/warranty terms when available, and the source/check time. Rank by the chosen criteria and explain the tradeoffs. Compare equivalent quantities/configurations and separate manufacturer specifications, reviewer findings, and retailer marketing claims.

Offer a small shortlist, an editable saved comparison, Refresh prices, and Open selected offer. Only describe a discount as historical savings when price-history evidence exists. A retailer's displayed markdown is identified as that retailer's claim. No background price tracking is implied. Purchases are completed by the user on the retailer's checkout.

Example: “Find a quiet wireless keyboard under my budget with USB-C charging and compare the best matches available to me.”

Acceptance: compare a known product set with variant mismatches, out-of-stock items, missing shipping, different currencies, bundles, refurbished condition, changed prices, and conflicting specifications. Verify ranking changes when the user changes a criterion. Live-check multiple retailer listings for an exact variant and confirm selected links reach the right product. Synthetic checkout cases ensure opening an offer never creates an order or claims one was placed.

## Phase 11 — YouTube mode

Purpose: discover useful videos, understand transcript content quickly, save what matters, and find the next useful video on a topic.

Accept a video URL, an explicitly attached current YouTube page, or a topic with preferred duration, level, language, and goals. Recommendations include verified video links, creator/title, duration/date when available, why each fits, and the available evidence behind that recommendation. Offer a local watchlist and mark-as-watched action; writing to the user's YouTube account is not assumed.

For summaries, obtain transcript text through a permitted available source: a supported user-initiated import of YouTube's visible transcript, an authorized caption source, or pasted/imported transcript text. Start with an explicit browser transcript-copy/import workflow; validate automatic retrieval separately before exposing it as available. Preserve available timestamps and language; process long transcripts in bounded sections with a coverage record, then produce a combined summary. Do not summarize only the first section while presenting it as the whole video.

Offer a short overview, main takeaways, timestamped jump links, an editable note, and Ask about this video. Summaries describe what the speaker says and distinguish recommendations/inferences. Save the video URL, title, creator, summary, timestamps, transcript provenance, and user notes locally; raw transcript retention is separately opt-in. Additional video recommendations can fill gaps or deepen a selected takeaway. Transcript-only understanding does not establish what appears visually on screen.

When a transcript is absent or incomplete, identify the coverage and offer transcript import or clearly labeled metadata-based recommendations. Never invent a transcript, a timestamp, or claim a video was watched. YouTube documents a visible transcript feature for captioned videos, while its captions download API requires permission to edit the video; the official API cannot be assumed to supply arbitrary public transcripts. [YouTube transcript help](https://support.google.com/youtube/answer/15930243?hl=en), [YouTube captions download reference](https://developers.google.com/youtube/v3/docs/captions/download).

Open timestamp links in the user's browser. Embedded viewing can be added only after a working native integration is verified; it is not required for transcript notes and recommendations. If embedding is selected, preserve the supported player interface and verify player errors and blocked autoplay; YouTube exposes playback controls through its official player API. [YouTube IFrame Player API](https://developers.google.com/youtube/iframe_api_reference).

Example: “Give me the main points from this Unity tutorial, save the useful timestamps, and find a good follow-up on the part about animation.”

Acceptance: compare summaries against a known timestamped transcript, covering key points, speaker attribution, quantities, and conclusions. Test short and long videos, pasted untimed transcripts, partial/missing captions, multiple languages, unavailable videos, edited notes, library search, and timestamp navigation. Verify recommended videos exist and fit the stated criteria. Run a real public-captioned-video import and summary without assuming the developer owns the video.

## Phase 12 — Music mode

Purpose: help the user listen to music, control the chosen player, discover songs/artists, and save discoveries while Little Guy looks like a music companion.

Spotify is the first supported service, as selected by the user. Build a provider adapter boundary rather than assuming a specific subscription. The baseline candidate is the explicitly selected Spotify desktop Windows media session plus verified Spotify track/artist links; test that exact combination before declaring support. If the user uses Spotify's web player, record and test that browser combination separately. Read only the selected session's supplied title/artist/playback metadata while Music mode or its explicitly enabled mini-player is active. Keep playback metadata local by default. Recommendations use the user's stated songs, artists, and preferences plus permitted research; verify applicable provider data-use rules before routing any provider-supplied content into model requests. Track changes do not initiate model requests.

Treat Spotify Web API control as a separately connected enhancement. Spotify's Start/Resume Playback endpoint requires Spotify Premium and the user-modify-playback-state scope; developer application quota/access rules also affect availability. Verify account eligibility, application registration, permitted endpoints, and distribution terms before choosing this route. If implemented, use a native-app authorization flow with PKCE and protected token storage, without embedding a client secret. Do not assume Spotify recommendation/audio-analysis endpoints are available to a new app. [Spotify playback reference](https://developer.spotify.com/documentation/web-api/reference/start-a-users-playback), [Spotify quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes), [Spotify authorization scopes](https://developer.spotify.com/documentation/web-api/concepts/scopes).

Windows exposes playback metadata and transport requests for media sessions that participating apps publish. This is a starting integration route, not proof of support for any particular music app or all controls. [Microsoft media-session overview](https://devblogs.microsoft.com/oldnewthing/20231108-00/?p=108980), [Windows play/pause API](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trytoggleplaypauseasync?view=winrt-26100).

Show a compact Now playing card with the selected service/session, track and artist when supplied, and only supported transport controls. Play/pause and next/previous target the chosen session and confirm the resulting state; multiple active media sessions require an explicit selection. Do not broadcast media keys and risk pausing an unrelated call or video. An unsupported control is explained, not simulated with a success animation.

Recommend from a chosen song, artist, mood, genre, activity, or explicit preference. Explain briefly why each recommendation fits. Distinguish studio/live/remix versions where identifiable, verify the track and artist identity, and provide a working link for the selected service. The user can save favorites, mark a recommendation unsuitable, and edit remembered preferences. A local discovery list is distinct from a provider playlist or queue.

A recommendation's Play action opens the exact supported service destination and confirms playback when the integration can observe it. If a provider requires the user's Play click or login, label the action Open in [service] and guide that step. One-click remote playback, queue insertion, and provider playlist writing are offered only after the selected service's supported APIs/account requirements are verified and connected. Do not silently introduce a paid API or describe a URL opening as successful playback.

Music mode must demonstrate actual listening with Spotify, accurate Now playing state, working play/pause, and discovery-to-listening handoff. Opening music search links alone does not pass. If Spotify integration needs additional access, present that concrete requirement rather than marking the mode complete. Other music providers are future adapter additions, not substitutes for the user-selected Spotify acceptance gate.

Headphones remain visible when music is paused; bobbing/notes follow a verified Playing state. Provide subtle/reduced-motion choices and keep the active player readable. Switching to Work can retain an explicitly enabled compact music card; entering Music does not autoplay. App pause stops Little Guy's research, voice, and ongoing metadata work. Pause music is a distinct user action; app pause does not send unsolicited transport commands to external players. Any app-owned embedded playback follows explicit playback/pause controls.

Example: “Play something mellow while I work, then show me a few artists like this one.” Little Guy helps start listening, shows the current track, and offers a short discovery list on request.

Acceptance: exercise real Spotify desktop playback and a synthetic media session; test Spotify web playback separately if advertised. Verify play/pause and each advertised control, track changes, unavailable metadata, stopped/closed player, multiple sessions, account/login failures, exact Spotify links, explicit-content preferences where verifiable, saved discoveries, and app pause/resume. If the optional API route is included, test expired/revoked authorization, rate limits, unavailable devices, and account restrictions. Confirm that playback animation stops on pause/error and that track changes do not automatically create model requests. Independently verify recommended track/artist identities and the handoff to actual listening.

## Phase 13 — integration, polish, and delivery

Test complete journeys across features: bookmark interrupted work and resume it; compare a change and investigate it with a walkthrough; save that walkthrough as a recipe; save a faster method as a tip; turn reference text into a checklist and use it in a focus session.

Add cross-mode journeys: bookmark a trip and resume its shortlist; compare headphones in Shopping then return to Music; save a YouTube tutorial's takeaways as a Work checklist; listen through the selected player during a focus session; switch modes while a search is still running. Confirm clear mode graphics and preservation of user edits throughout.

Polish empty/loading/error states, typography, spacing, small-screen placement, focus behavior, screen scaling, keyboard-only navigation, Narrator labels, and reduced motion. Add restrained companion gestures for saving, comparing, and completing work, while keeping controls and information easy to read. Avoid adding lengthy animation delays to quick actions.

Run the existing answer, bubble, voice, walkthrough, annotation, reply-drafting, hide/pause/quit, and account/transport regressions. Include repeated activation, cancellation, network interruption, late responses, restart, and a sustained mixed-feature session of at least two hours. Check for duplicate submissions, retained temporary captures, unbounded memory growth, and orphaned owned processes.

Use Windows test applications and synthetic documents for repeatable baselines. Test Ableton and Unity workflows when those apps are installed and accessible; list tested versions. Required native UI checks must be performed, not inferred from unit tests. If hardware, app access, or physical input coverage is unavailable, leave that gate explicitly unverified and request only the specific assistance needed. Never claim universal app compatibility.

Deliver a versioned portable Windows x64 ZIP with its executable and dependencies, updated README/privacy/testing documentation, shortcut reference, a mode/personality contact sheet, screenshots or a short demo, and a feature-by-feature and mode-by-mode verification record. Document supported services and versions, required connections, booking handoff behavior, transcript coverage, and actual playback support. Launch the packaged output itself to validate the handoff. Share the local artifact with the user; public hosting, installer signing, and automatic updates are separate distribution work.

## Agent execution and tester gates

After approval, the lead coordinates up to three agents alongside its own integration work:

| Role | Responsibility |
|---|---|
| Lead | Own the plan, integration files, task boundaries, progress reporting, issue triage, and packaged delivery |
| Feature builder | Implement the current approved feature and fix defects reported by testing |
| Supporting builder/reviewer | Handle an independent module, integration adapter, or personality graphics for the current phase, or review UI details without overlapping edits |
| Independent tester | Derive tests from acceptance criteria, exercise the actual build, collect evidence, and issue pass/fail |

Execute phases in the order above. Parallel work is permitted within the current phase, but builders are not assigned the next feature until the tester verifies the current phase and the lead accepts the evidence. Test design can run while that feature is implemented. Agents report their current scope, estimated completion, changed files, validation results, blockers, and the next concrete action to the lead. They do not independently expand scope or start the next phase.

Every phase follows this loop:

1. Define a small testable slice and its acceptance scenarios. Assign explicit file ownership and use the current working tree safely.
2. Implement the behavior, integrate it into the real UI, and run targeted developer checks.
3. The independent tester exercises user journeys, failures, and related regressions on the exact candidate build; capture build identity, steps, expected/actual results, and evidence.
4. A failure returns to the responsible builder with a reproducible issue. Fix it, then retest the failure and the affected regression area. Mark unavailable checks unverified, never passed.
5. The lead reviews usability and evidence. Only a verified candidate advances to the next phase. Any relevant later integration change invalidates the affected earlier sign-off and triggers focused retesting.

Meaningful tests cover user behavior and state boundaries, rather than merely repeating implementation logic. Automated checks, synthetic fixtures, live-model checks using synthetic content, and native end-to-end checks are recorded separately. During an approval or environment blocker, continue independent work within the current phase where possible, without bypassing its gate.

Completion requires all six productivity features, all four new modes, the mode/personality framework, and shortcut configuration to pass their required gates; cross-feature and cross-mode journeys to pass; the packaged app to run; and no known defects that block promised workflows, lose data, invoke the wrong command/target, falsely claim an offer/booking/playback result, or defeat cancellation. Remaining minor issues and untested environments are documented with their actual impact. Iterate on concrete tester and usability findings until these conditions are met; building successfully alone is insufficient. Test evidence must distinguish live research from fixtures, assisted booking from actual purchases, transcript summaries from metadata-only notes, and real media control from opening a link.

## Progress and pause/resume contract

All progress updates begin with an estimated completion percentage in square brackets. Report the current phase, what is verified, what is being fixed, and the next gate. While work is active, keep the user informed at least once per minute, including while agents are busy. Estimated percentages never substitute for tester pass/fail records.

Maintain a local execution ledger after approval with phase status, assignments, build/test evidence, open defects, and resume instructions. This is the source of truth if conversation context changes.

If the user says pause:

1. Immediately stop assigning work and interrupt all agents. Stop active builds/tests/helpers where safely interruptible, and do not start new operations.
2. Preserve current edits and record the smallest necessary checkpoint. An already-running operation that cannot be canceled may finish; its completion must not trigger follow-on work.
3. Report the paused state and any operation still winding down. Leave agents idle. Do not schedule a background continuation or automatically resume because time passed.
4. Resume only after the user's instruction. Reconcile the workspace and prior test evidence, then continue from the recorded checkpoint and rerun checks invalidated by interruption or intervening changes.

The user's approval authorizes implementation and verification of this plan. It does not waive future pauses. Until approval, only this plan is prepared; no implementation agents or feature work start.
