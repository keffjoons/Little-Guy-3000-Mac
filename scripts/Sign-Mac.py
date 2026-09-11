#!/usr/bin/env python3
"""Reuse a private local signing identity so macOS can recognize app updates."""
import fcntl
import os
from pathlib import Path
import secrets
import shlex
import subprocess
import sys
import tempfile

os.umask(0o077)
DIRECTORY = Path.home() / 'Library/Application Support/LittleGuy3000/signing'
KEYCHAIN = DIRECTORY / 'development.keychain-db'
PASSWORD = DIRECTORY / 'keychain-password'
CERTIFICATE = DIRECTORY / 'certificate.pem'


def run(*args, input=None):
    result = subprocess.run(args, input=input, text=True, capture_output=True)
    if result.returncode:
        # Never include command arguments: some Security CLI operations take a password.
        raise RuntimeError(f'{Path(args[0]).name} failed: {result.stderr.strip()}')
    return result.stdout


def provision():
    if KEYCHAIN.exists() and PASSWORD.exists() and CERTIFICATE.exists():
        return
    if any(p.exists() for p in (KEYCHAIN, PASSWORD, CERTIFICATE)):
        raise RuntimeError(f'Incomplete signing identity in {DIRECTORY}. Restore it; do not replace it with a new identity.')
    password = secrets.token_urlsafe(48)
    PASSWORD.write_text(password)
    original_search = shlex.split(run('/usr/bin/security', 'list-keychains', '-d', 'user'))
    try:
        run('/usr/bin/security', 'create-keychain', '-p', password, str(KEYCHAIN))
        with tempfile.TemporaryDirectory(dir=DIRECTORY) as temporary:
            temp = Path(temporary)
            config = temp / 'openssl.cnf'
            config.write_text('''[req]
prompt = no
distinguished_name = subject
x509_extensions = codesign
[subject]
CN = Little Guy Local Development
[codesign]
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,codeSigning
''')
            run('/usr/bin/openssl', 'req', '-new', '-x509', '-newkey', 'rsa:3072',
                '-nodes', '-sha256', '-days', '3650', '-config', str(config),
                '-keyout', str(temp / 'key.pem'), '-out', str(temp / 'certificate.pem'))
            run('/usr/bin/openssl', 'pkcs12', '-export', '-inkey', str(temp / 'key.pem'),
                '-in', str(temp / 'certificate.pem'), '-out', str(temp / 'identity.p12'),
                '-passout', 'file:' + str(PASSWORD))
            run('/usr/bin/security', 'import', str(temp / 'identity.p12'), '-k', str(KEYCHAIN),
                '-P', password, '-x', '-T', '/usr/bin/codesign')
            # This dedicated keychain contains only Little Guy's signing key.
            run('/usr/bin/security', 'set-key-partition-list', '-S', 'apple-tool:,apple:,codesign:',
                '-s', '-k', password, str(KEYCHAIN))
            CERTIFICATE.write_bytes((temp / 'certificate.pem').read_bytes())
    finally:
        # Creating a keychain can add it to the search list. Remove only our addition;
        # preserve any other change another application made during provisioning.
        current = shlex.split(run('/usr/bin/security', 'list-keychains', '-d', 'user'))
        if str(KEYCHAIN) not in original_search and str(KEYCHAIN) in current:
            run('/usr/bin/security', 'list-keychains', '-d', 'user', '-s',
                *(entry for entry in current if entry != str(KEYCHAIN)))


def main():
    if len(sys.argv) != 2:
        raise RuntimeError('Usage: Sign-Mac.py path-to-app')
    app = Path(sys.argv[1]).resolve()
    if not (app / 'Contents/Info.plist').is_file():
        raise RuntimeError('Expected a built app bundle')
    DIRECTORY.mkdir(parents=True, exist_ok=True, mode=0o700)
    with (DIRECTORY / '.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        original_search = shlex.split(run('/usr/bin/security', 'list-keychains', '-d', 'user'))
        try:
            provision()
            run('/usr/bin/security', 'list-keychains', '-d', 'user', '-s', str(KEYCHAIN), *original_search)
            password = PASSWORD.read_text()
            run('/usr/bin/security', 'unlock-keychain', '-p', password, str(KEYCHAIN))
            fingerprint = run('/usr/bin/openssl', 'x509', '-in', str(CERTIFICATE),
                              '-noout', '-fingerprint', '-sha1').strip().split('=')[1].replace(':', '')
            requirement = f'=designated => identifier "com.littleguy3000.mac" and certificate leaf = H"{fingerprint}"'
            run('/usr/bin/codesign', '--force', '--sign', fingerprint, '--keychain', str(KEYCHAIN),
                '--identifier', 'com.littleguy3000.mac', '--requirements', requirement, str(app))
            run('/usr/bin/codesign', '--verify', '--strict', str(app))
            print('Signed with persistent Little Guy local identity; existing identity reused on subsequent builds.')
        finally:
            current = shlex.split(run('/usr/bin/security', 'list-keychains', '-d', 'user'))
            if str(KEYCHAIN) not in original_search:
                run('/usr/bin/security', 'list-keychains', '-d', 'user', '-s', *(entry for entry in current if entry != str(KEYCHAIN)))
            if KEYCHAIN.exists():
                run('/usr/bin/security', 'lock-keychain', str(KEYCHAIN))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'Signing stopped: {error}', file=sys.stderr)
        sys.exit(1)
