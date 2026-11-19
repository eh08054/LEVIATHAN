#!/usr/bin/env bash
S="$(dirname "$0")"
for i in $(seq 1 60); do R=$("$S/mcp.sh" resources/read '{"uri":"mcpforunity://editor/state"}'); echo "$R" | grep -q 'ready_for_tools\\": *true' && exit 0; sleep 3; done
echo "timeout waiting for editor"
