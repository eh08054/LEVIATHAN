#!/usr/bin/env bash
S="$(dirname "$0")"
powershell -NoProfile -Command '$p = Get-Process Unity | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1; (New-Object -ComObject WScript.Shell).AppActivate($p.Id) | Out-Null' >/dev/null
for i in 1 2 3 4 5 6 7 8; do
  OUT=$("$S/call.sh" manage_ui '{"action":"render_ui","target":"SectorUI","width":1920,"height":1080}')
  P=$(echo "$OUT" | grep -o '"fullPath":"[^"]*"' | cut -d'"' -f4)
  if [ -n "$P" ]; then echo "$P"; exit 0; fi
  sleep 1
done
echo "capture failed: $OUT"
