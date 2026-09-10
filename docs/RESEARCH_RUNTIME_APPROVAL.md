# Proposed research runtime change

The live test confirms that the current Codex runtime has no available web-search tool. Report generation alone works, but the app refuses to call it researched information without a completed search event.

Proposed next test: enable `features.code_mode` and `features.code_mode_host` only for research threads, alongside `web_search = "live"`. This adds the local JavaScript orchestration runner that can call available tools. It is a broader runtime than the current explanation-only configuration; web availability still needs verification.

Keep the existing empty environment list, read-only sandbox, no approval escalation, no selected capability roots, and disabled shell, file/image access tools, browser/computer control, plugins, connectors, multi-agent, and hooks. Google uploads remain explicit typed C# code with the narrow `drive.file` scope. Normal explanations retain their existing configuration.

Automatic approval review rejected the runtime expansion. It has not been applied. User approval is needed before testing this proposal. If the test works, verify tool events, source links, cancellation, and isolation before shipping it.
