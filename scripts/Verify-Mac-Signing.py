#!/usr/bin/env python3
"""Verify different bundle contents retain the same certificate-bound identity."""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent
source = root / 'artifacts/Little Guy 3000.app'

def command(*args):
    result = subprocess.run(args, check=True, capture_output=True, text=True)
    return result.stdout + result.stderr

def signature(app):
    info = command('/usr/bin/codesign', '-d', '-r-', '--verbose=4', str(app))
    return (re.search(r'^designated => (.+)$', info, re.M)[1],
            re.search(r'^CDHash=(.+)$', info, re.M)[1])

with tempfile.TemporaryDirectory(prefix='signing-check-', dir=root / '.local') as directory:
    updated = Path(directory) / 'Little Guy 3000.app'
    shutil.copytree(source, updated)
    before, old_hash = signature(source)
    (updated / 'Contents/Resources/signing-update-test.txt').write_text('Different update contents\n')
    command('/usr/bin/python3', str(root / 'scripts/Sign-Mac.py'), str(updated))
    after, new_hash = signature(updated)
    assert before == after and 'certificate leaf' in after
    assert old_hash != new_hash, 'The test must change the code directory hash'
    command('/usr/bin/codesign', '--verify', '--strict', '-R=' + before, str(updated))
    print('PASS: changed bundle hash, unchanged certificate-bound identity; updated bundle satisfies the previous build requirement')
