# Mac issue fixes

Scope: open issues labeled `mac`, checked on 2026-10-07. These fixes are local;
no upstream issue has been closed or commented on.

| Issue | Change | Verification |
| --- | --- | --- |
| [114](https://github.com/yksr-melt/Meltype/issues/114) | Cache positive and negative macOS spelling results, up to 4096 entries, with five-minute expiry. Preserve the converter's incremental state. | The reported long input produced the same output twice. First run: 237 spelling calls; repeated run: zero. |
| [101](https://github.com/yksr-melt/Meltype/issues/101) | Replace shell array appends with native input-source registration. Normalize Meltype's parent/mode records, preserve other sources, and back up the old list. | A 15-duplicate fixture becomes one parent plus one mode; normalization is idempotent. Live registration has one of each. |
| [77](https://github.com/yksr-melt/Meltype/issues/77) | Recognize an English verb before a Japanese conjugation even when a romaji token crosses the boundary. | Native converter returns `reflectされた` for `reflectsareta`. Core tests cover conjugations and unchanged English words. |
| [118](https://github.com/yksr-melt/Meltype/issues/118) | Update source version to 1.0.3; use one build version for native code and the app plist; derive converter metadata from the plist. | Installed plist and core build property both read 1.0.3. Release-tag builds override both with the tag version. |
| [40](https://github.com/yksr-melt/Meltype/issues/40) | Add an input-menu action to merge exported macOS plist dictionaries into the local user dictionary and reload sessions. | XML and binary fixtures preserve existing entries, normalize katakana readings, and skip duplicates on reimport. |

Core regression suite: 198/198 passed. Installed-app `--self-test` passed.
App signature verification and shell syntax checks passed.

Limits: the original System Settings UI with 15 entries has not been reproduced
on the reported OS version. Tests exercise plist parsing and merging, not the
file-picker UI or a user's actual exported dictionary. Spelling changes made in
macOS may take up to five minutes to appear because of the cache. The existing
quality corpus still has two unrelated mismatches (`my name is taro` and
`apinoerror`); they are not regressions from these fixes.
