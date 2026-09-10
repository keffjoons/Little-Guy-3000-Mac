#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
mkdir -p .local/swift-cache
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift tests/Mac/ProtocolTests.swift \
    -o .local/mac-protocol-tests
.local/mac-protocol-tests
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CompanionSession.swift tests/Mac/SessionTests.swift \
    -o .local/mac-session-tests
.local/mac-session-tests
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/ImageAttachment.swift tests/Mac/ImageTests.swift \
    -o .local/mac-image-tests
.local/mac-image-tests
if [ "${1:-}" = "--codex-fixture" ]; then
    python3 scripts/Verify-Codex.py
fi
if [ "${1:-}" = "--live" ]; then
    xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
        src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CompanionSession.swift tests/Mac/LiveSessionTests.swift \
        -o .local/mac-live-tests
    .local/mac-live-tests
fi
