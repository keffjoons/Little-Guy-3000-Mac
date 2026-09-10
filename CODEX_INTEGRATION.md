# Codex integration

Little Guy 3000 pins codex-cli 0.153.4. It starts an owned app-server process with a dedicated CODEX_HOME, empty workspace, read-only sandbox, no approval channel, no dynamic tools, no environments or selected capability roots, and ephemeral threads. A Windows job object kills the child on app exit. Existing desktop credentials are not imported; browser-managed account sign-in and the OS keyring are used.

Model availability and reasoning efforts come from model/list. Quick questions, reply drafts and overviews use GPT-6 Astra Low; full-panel questions use the configured Medium/High preference. Structured answer schemas, cancellation generations, current capture IDs and bounded output guard the UI. Reply batches and interface overviews start fresh threads where required. No screenshot text is command authority. New local voice commands use a fixed parser/executor independent of Codex and never enable backend computer tools.

See scripts/Test.ps1 and TESTING.md for fixtures, live synthetic probes, limitations and protocol evidence. Comprehensive backend retention auditing and public-release support decisions remain open.
