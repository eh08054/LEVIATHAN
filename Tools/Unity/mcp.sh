#!/usr/bin/env bash
# usage: mcp.sh <method> '<params json>'
# Talks to the UnityMCP bridge over HTTP. Opens (and caches) an MCP session when needed.
D="$(dirname "$0")"
URL=http://127.0.0.1:8080/mcp
H=(-H "Content-Type: application/json" -H "Accept: application/json, text/event-stream")
open_session() {
  curl -s -D "$D/.session" -o /dev/null -X POST "$URL" "${H[@]}" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"unity-helper","version":"1"}}}'
  SID=$(grep -i mcp-session-id "$D/.session" | awk '{print $2}' | tr -d '\r')
  curl -s -o /dev/null -X POST "$URL" "${H[@]}" -H "mcp-session-id: $SID" -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'
}
[ -f "$D/.session" ] || open_session
SID=$(grep -i mcp-session-id "$D/.session" | awk '{print $2}' | tr -d '\r')
send() { curl -s -X POST "$URL" "${H[@]}" -H "mcp-session-id: $SID" -d "{\"jsonrpc\":\"2.0\",\"id\":$RANDOM,\"method\":\"$1\",\"params\":${2:-{\}}}"; }
OUT=$(send "$1" "$2")
if [ -z "$OUT" ] || echo "$OUT" | grep -qi 'session'; then
  if ! echo "$OUT" | grep -q '^data: {"jsonrpc":"2.0","id":[0-9]*,"result"'; then
    open_session; SID=$(grep -i mcp-session-id "$D/.session" | awk '{print $2}' | tr -d '\r'); OUT=$(send "$1" "$2")
  fi
fi
echo "$OUT" | sed -n 's/^data: //p'
