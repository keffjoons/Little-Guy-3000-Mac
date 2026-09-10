#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
mkdir -p .local/swift-cache
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift tests/Mac/ProtocolTests.swift \
    -o .local/mac-protocol-tests
.local/mac-protocol-tests
if [ "${1:-}" = "--codex-fixture" ]; then
    python3 scripts/Verify-Codex.py
fi
