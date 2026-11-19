#!/usr/bin/env bash
# usage: call.sh <tool> '<args json>'
"$(dirname "$0")/mcp.sh" tools/call "{\"name\":\"$1\",\"arguments\":${2:-{\}}}" | python -c "import json,sys
for line in sys.stdin:
  line=line.strip()
  if not line: continue
  d=json.loads(line)
  if 'error' in d: print('ERROR',d['error']); continue
  for c in d['result'].get('content',[]):
    print(c.get('text',c))"
