#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
APP="$ROOT/artifacts/Little Guy 3000.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$ROOT/.local/swift-cache"
python3 - "$ROOT" "$APP" <<'PY'
import pathlib, plistlib, re, shutil, sys
root, app = map(pathlib.Path, sys.argv[1:])
# Reuse the Windows guide's restrictions instead of maintaining a divergent policy.
source = (root/'src/LittleGuy3000.Codex/CodexClient.cs').read_text()
match = re.search(r'public const string Text = """\n(.*?)\n\s*""";', source, re.S)
if not match: raise SystemExit('Guide configuration not found')
config = '\n'.join(line.strip() for line in match.group(1).splitlines()) + '\n'
(app/'Contents/Resources/guide-config.toml').write_text(config)
shutil.copyfile(root/'src/LittleGuy3000.Desktop/Assets/Square150x150Logo.scale-200.png', app/'Contents/Resources/LittleGuy.png')
info = dict(CFBundleExecutable='LittleGuy3000', CFBundleIdentifier='com.littleguy3000.mac',
            CFBundleName='Little Guy 3000', CFBundleDisplayName='Little Guy 3000', CFBundleIconFile='LittleGuy.png',
            CFBundlePackageType='APPL', CFBundleShortVersionString='0.4.0', CFBundleVersion='5',
            LSMinimumSystemVersion='14.0', NSHighResolutionCapable=True,
            NSScreenCaptureUsageDescription='See the window under your pointer when you ask Little Guy a question.',
            NSMicrophoneUsageDescription='Listen while you hold the shortcut or use the Talk button.',
            NSSpeechRecognitionUsageDescription='Turn your spoken question into text using on-device speech recognition.')
with (app/'Contents/Info.plist').open('wb') as f: plistlib.dump(info, f)
PY
xcrun swiftc -swift-version 5 -O -target "$(uname -m)-apple-macosx14.0" \
    -module-cache-path "$ROOT/.local/swift-cache" \
    -framework AppKit -framework Carbon \
    src/LittleGuy3000.Mac/*.swift -o "$APP/Contents/MacOS/LittleGuy3000"
codesign --force --sign - --identifier com.littleguy3000.mac "$APP"
codesign --verify --strict "$APP"
printf 'Built: %s\n' "$APP"
