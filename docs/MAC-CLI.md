# Mac CLI build

Update the installed input method from the local source in one command:

```bash
bash /Users/hiromichi/Documents/github/other/Meltype/mac/update.sh
```

This runs the core tests, builds and installs the app, runs the bundled Mac
input checks, and selects Meltype again. It restores the input method even if
the input checks fail. It does not pull Git changes or overwrite the user dictionary.

Build and install the Mac input method from Terminal:

```bash
cd /Users/hiromichi/Documents/github/other/Meltype
mac/build-cli.sh --setup --all
```

The default `--all` flow runs the core tests, builds `mac/build/Meltype.app`,
and installs it into:

```text
~/Library/Input Methods/Meltype.app
```

Useful shorter commands:

```bash
mac/build-cli.sh --test
mac/build-cli.sh --build
mac/build-cli.sh --install
```

After the first install, log out and log in again. Then choose Meltype from the
menu bar input menu. If it does not appear, add it from:

```text
System Settings -> Keyboard -> Input Sources -> Edit... -> + -> Japanese -> Meltype
```

Debug logs:

```bash
log stream --predicate 'process == "Meltype"' --level debug
```

Run the installed app's offline regression checks:

```bash
"$HOME/Library/Input Methods/Meltype.app/Contents/MacOS/Meltype" --self-test
```

The checks use the real bundled converter and cover mixed input, English,
emoji candidates, spelling suggestions, input-source deduplication, and plist
dictionary import. They do not exercise application UI controls.

To repair duplicate Meltype input-source records:

```bash
"$HOME/Library/Input Methods/Meltype.app/Contents/MacOS/Meltype" --register-input-source
```

Registration preserves other input sources. The original list is backed up in
the `MeltypeInputSourcesBeforeRepair` preference in `com.apple.HIToolbox`.

Import an exported macOS dictionary using the Meltype input menu's dictionary
import command. XML and binary plists containing `shortcut`/`phrase` or
`reading`/`word` records are supported. Existing entries are retained and exact
duplicates are skipped. Readings shorter than two UTF-16 characters, and entries
containing tabs or line breaks, are unsupported. Active sessions reload after
import. No dictionary data is sent over the network.

Build versions come from the core project's `Version`, `MELTYPE_VERSION`, or
the release tag in GitHub Actions. The native build and app plist use the same
value; the converter reads its version from the app plist.
