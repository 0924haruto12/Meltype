#!/usr/bin/env bash
# Start in the GUI launchd session so terminal teardown cannot stop the IME.
set -euo pipefail
app="${1:?Usage: start-input-method.sh /path/to/Meltype.app}"
label=io.github.yksr-melt.Meltype.manual
log_dir="${MELTYPE_START_LOG_DIR:-$HOME/Library/Logs}"
mkdir -p "$log_dir"
launchctl remove "$label" 2>/dev/null || true
launchctl submit -l "$label" -o "$log_dir/Meltype.stdout.log" -e "$log_dir/Meltype.stderr.log" -- "$app/Contents/MacOS/Meltype"
for _ in {1..30}; do
    state="$(launchctl list "$label" 2>/dev/null || true)"
    if [[ "$state" == *'"PID" ='* && "$state" == *'Meltype_Connection'* ]]; then
        echo "Meltype input server is running."
        exit 0
    fi
    sleep 0.2
done
echo "Meltype input server did not become ready. Keep the standard input source selected." >&2
exit 1
