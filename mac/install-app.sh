#!/usr/bin/env bash
# Prepare and verify the new bundle before stopping or replacing the current IME.
set -euo pipefail
source_app="${1:?Usage: install-app.sh source.app target.app}"
target_app="${2:?Usage: install-app.sh source.app target.app}"
[[ -d "$source_app" ]] || { echo "Missing app: $source_app" >&2; exit 1; }
mkdir -p "$(dirname "$target_app")"
# Never stage backup/new bundles in Input Methods: macOS scans that directory.
stage="$(mktemp -d "${TMPDIR:-/tmp}/meltype-install.XXXXXX")"
cleanup() {
    local result=$?
    trap - EXIT
    if [[ -d "$stage/previous.app" && ! -e "$target_app" ]]; then
        if ! mv "$stage/previous.app" "$target_app"; then
            echo "Restore the previous app from $stage/previous.app" >&2
            exit 1
        fi
    fi
    for staged_app in "$stage/Meltype.app" "$stage/previous.app"; do
        /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -u "$staged_app" 2>/dev/null || true
    done
    rm -rf "$stage"
    exit "$result"
}
trap cleanup EXIT
ditto "$source_app" "$stage/Meltype.app"
codesign --verify --deep --strict "$stage/Meltype.app"
pkill -x Meltype 2>/dev/null || true
if [[ -e "$target_app" ]]; then mv "$target_app" "$stage/previous.app"; fi
mv "$stage/Meltype.app" "$target_app"
