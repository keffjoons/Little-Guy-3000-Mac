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
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CompanionSession.swift \
    src/LittleGuy3000.Mac/ImageAttachment.swift src/LittleGuy3000.Mac/PointerCapture.swift \
    src/LittleGuy3000.Mac/VoiceInput.swift src/LittleGuy3000.Mac/QuickCompanion.swift \
    tests/Mac/QuickTests.swift -o .local/mac-quick-tests
.local/mac-quick-tests
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CodexVoicePlayer.swift \
    src/LittleGuy3000.Mac/SpeechController.swift tests/Mac/SpeechTests.swift -o .local/mac-speech-tests
.local/mac-speech-tests
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CodexVoicePlayer.swift \
    src/LittleGuy3000.Mac/LiveConversation.swift src/LittleGuy3000.Mac/WindowActions.swift \
    src/LittleGuy3000.Mac/PointerCapture.swift src/LittleGuy3000.Mac/ImageAttachment.swift \
    tests/Mac/RealtimeTests.swift -o .local/mac-realtime-tests
.local/mac-realtime-tests
xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
    src/LittleGuy3000.Mac/QuickTranscript.swift tests/Mac/TranscriptTests.swift -o .local/mac-transcript-tests
.local/mac-transcript-tests
if [ "${1:-}" = "--realtime" ]; then
    /usr/bin/say -v Samantha -o .local/live-first.aiff 'Please count slowly from one to fifty.'
    /usr/bin/afconvert -f WAVE -d LEI16 .local/live-first.aiff .local/live-first.wav
    /usr/bin/say -v Samantha -o .local/live-interrupt.aiff 'Stop. Say only peach.'
    /usr/bin/afconvert -f WAVE -d LEI16 .local/live-interrupt.aiff .local/live-interrupt.wav
    xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
        src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CodexVoicePlayer.swift \
        src/LittleGuy3000.Mac/LiveConversation.swift src/LittleGuy3000.Mac/WindowActions.swift \
        src/LittleGuy3000.Mac/PointerCapture.swift src/LittleGuy3000.Mac/ImageAttachment.swift \
        tests/Mac/RealtimeLiveTests.swift -o .local/mac-realtime-live-tests
    .local/mac-realtime-live-tests
    /usr/bin/say -v Samantha -o .local/live-screen.aiff 'What colour is the square on my screen, and what verification code is shown?'
    /usr/bin/afconvert -f WAVE -d LEI16 .local/live-screen.aiff .local/live-screen.wav
    xcrun swiftc -swift-version 5 -module-cache-path "$ROOT/.local/swift-cache" \
        src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CodexVoicePlayer.swift \
        src/LittleGuy3000.Mac/LiveConversation.swift src/LittleGuy3000.Mac/WindowActions.swift \
        src/LittleGuy3000.Mac/PointerCapture.swift src/LittleGuy3000.Mac/ImageAttachment.swift \
        tests/Mac/RealtimeHandoffTests.swift -o .local/mac-realtime-handoff-tests
    .local/mac-realtime-handoff-tests
    /usr/bin/say -v Samantha -o .local/live-playlist.aiff 'How do I make a new playlist?'
    /usr/bin/afconvert -f WAVE -d LEI16 .local/live-playlist.aiff .local/live-playlist.wav
    /usr/bin/say -v Samantha -o .local/live-window-change.aiff 'What background colour and heading am I looking at now?'
    /usr/bin/afconvert -f WAVE -d LEI16 .local/live-window-change.aiff .local/live-window-change.wav
    .local/mac-realtime-handoff-tests --playlist
fi
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
        src/LittleGuy3000.Mac/CodexConnection.swift src/LittleGuy3000.Mac/CompanionSession.swift \
        src/LittleGuy3000.Mac/CodexVoicePlayer.swift src/LittleGuy3000.Mac/SpeechController.swift \
        tests/Mac/LiveVoiceTests.swift -o .local/mac-live-voice-tests
    .local/mac-live-voice-tests
fi
