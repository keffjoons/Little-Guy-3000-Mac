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
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/ImageAttachment.swift \
    src/LittleGuy3000.Mac/ScreenCapturePicker.swift tests/Mac/ScreenFrameTests.swift -o .local/mac-screen-frame-tests
.local/mac-screen-frame-tests
if [ "${1:-}" = "--codex-fixture" ]; then
    python3 scripts/Verify-Codex.py
fi
if [ "${1:-}" = "--live" ]; then
    xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
        src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CompanionSession.swift tests/Mac/LiveSessionTests.swift \
        -o .local/mac-live-tests
    .local/mac-live-tests
fi
if [ "${1:-}" = "--voice" ]; then
    xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
        src/LittleGuy3000.Mac/SpeechController.swift tests/Mac/SpeechTests.swift -o .local/mac-speech-tests
    .local/mac-speech-tests
fi
