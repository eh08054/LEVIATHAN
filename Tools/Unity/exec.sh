#!/usr/bin/env bash
S="$(dirname "$0")"
ARGS=$(python -c "import json,sys; print(json.dumps({'action':'execute','code':open(sys.argv[1],encoding='utf-8').read()}))" "$1")
"$S/call.sh" execute_code "$ARGS"
