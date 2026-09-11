#!/usr/bin/env python3
"""Deterministic stdio fixture. Never contacts a network or reads user credentials."""
import json
import os
import sys

assert sys.argv[1:] == ['app-server', '--listen', 'stdio://']
waiting = {}

def send(value):
    data = (json.dumps(value, ensure_ascii=False) + '\n').encode()
    # Deliberately split JSON across writes, including possible UTF-8 boundaries.
    for offset in range(0, len(data), 3):
        sys.stdout.buffer.write(data[offset:offset + 3])
        sys.stdout.buffer.flush()

for line in sys.stdin:
    value = json.loads(line)
    method, identity = value.get('method'), value.get('id')
    if method == 'initialize':
        send({'id': identity, 'result': {'userAgent': 'fixture 🌱'}})
    elif method == 'account/read':
        send({'id': identity, 'result': {'isolated': os.getcwd() == os.path.join(os.environ['CODEX_HOME'], 'empty-workspace')
                                       and 'OPENAI_API_KEY' not in os.environ}})
    elif method in ('test/approval', 'test/tool'):
        request_id = 'approval' if method == 'test/approval' else 'tool'
        waiting[request_id] = identity
        send({'id': request_id, 'method': 'item/commandExecution/requestApproval' if request_id == 'approval' else 'item/tool/call', 'params': {}})
    elif method == 'test/action':
        waiting['action'] = identity
        send({'id': 'action', 'method': 'item/tool/call', 'params': {'threadId': 'live', 'turnId': 'turn', 'callId': 'fixture', 'tool': 'inspect_window', 'arguments': {}}})
    elif method == 'test/app-access':
        waiting['access'] = identity
        send({'id': 'access', 'method': 'mcpServer/elicitation/request', 'params': {'threadId': 'live', 'serverName': 'cua_repl'}})
    elif identity == 'access' and identity in waiting:
        send({'id': waiting.pop(identity), 'result': value.get('result', {})})
    elif identity == 'action' and identity in waiting:
        send({'id': waiting.pop(identity), 'result': value.get('result', {})})
    elif identity in waiting:
        result = {'declined': value.get('result', {}).get('decision') == 'decline'} if identity == 'approval' else {'rejected': value.get('error', {}).get('code') == -32601}
        send({'id': waiting.pop(identity), 'result': result})
    elif method == 'test/error':
        send({'id': identity, 'error': {'code': -42, 'message': 'not exposed'}})
