#!/usr/bin/env bash
# Game view screenshot at 2x (sharper than render_ui): shot.sh <name> -> prints the PNG path.
S="$(dirname "$0")"
powershell -NoProfile -Command '$p = Get-Process Unity | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1; (New-Object -ComObject WScript.Shell).AppActivate($p.Id) | Out-Null' >/dev/null
rm -f "Assets/Screenshots/$1.png"
"$S/call.sh" manage_camera "{\"action\":\"screenshot\",\"screenshot_super_size\":2,\"screenshot_file_name\":\"$1\"}" >/dev/null
for i in 1 2 3 4 5 6 7 8 9 10; do [ -f "Assets/Screenshots/$1.png" ] && break; sleep 1; done
sleep 1
echo "$(pwd)/Assets/Screenshots/$1.png"
