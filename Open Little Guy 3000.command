#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/artifacts/Little Guy 3000.app"
if [ ! -x "$APP/Contents/MacOS/LittleGuy3000" ]; then
    /bin/bash "$ROOT/scripts/Build-Mac.sh"
fi
open "$APP"
