#!/usr/bin/env bash
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
app="$HOME/Library/Input Methods/Meltype.app"

case "${1:-}" in
    -h|--help)
        echo "Usage: bash mac/update.sh"
        echo "Test, build, install, verify, and select the local Mac input method."
        exit 0 ;;
    "") ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
esac
[[ $# -le 1 ]] || { echo "Too many arguments" >&2; exit 1; }
[[ "$(uname)" == Darwin ]] || { echo "This updater requires macOS." >&2; exit 1; }

bash "$here/build-cli.sh" --test --install

# Native checks exit the input-method process; restore it even if a check fails.
restore_input() {
    local result=$?
    trap - EXIT
    if ! "$app/Contents/MacOS/Meltype" --register-input-source ||
       ! open -g "$app" ||
       ! swift "$here/select-input-source.swift"; then
        echo "Could not select Meltype. Choose it from the input menu." >&2
        [[ $result -ne 0 ]] || result=1
    fi
    exit "$result"
}
trap restore_input EXIT
"$app/Contents/MacOS/Meltype" --check-inputs "$here/Resources/InputChecks.tsv"
echo "Meltype update and input checks completed."
