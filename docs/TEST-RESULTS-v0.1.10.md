# v0.1.10 — Explorer compatibility paths and semantic frame/clip types

## Scope / honesty

User reported that File Explorer did not stash. Two probable code-level incompatibilities were found:
strict HTCAPTION-only re-hit testing, and treating child-origin move events as root windows.
No claim of root cause confirmation: an empty-directory Explorer probe could not identify its new
window uniquely using its known fixture title. It stopped rather than enumerate existing folders/windows.
Real Explorer titlebar dragging remains BLOCKED / unverified; the user can use the new diagnostic popup.

## Implemented

- Modern HTCLIENT header support and bounded hit-test-timeout geometry fallback, with frame/button/body exclusion.
- Move-event child HWND normalized to root, but child destroy never normalized to avoid deleting a living root card.
- Capture pointer/button state at event delivery; bounded 120ms wait if end precedes button-up state.
- Pointer-follow compatibility path when move-start/end is absent: arm only in validated header of clicked foreground
  window, match process identity, translation must follow held pointer, reject resize/programmatic movement.
- Polling never stashes while held. Release remains the sole commit; Escape/target/geometry/permissions still checked.
- Diagnostic text in memory reports the gate only, never private window title/path or content.
- HangingTheme maps Image / Window / FolderWindow to paper, frame, title band, ink, clip silhouette and feedback palette.
- Image: warm photo-paper + narrow wood peg; software: blue-gray titlebar frame + wide silver binder;
  recognized Explorer: folder-label frame + brass binder. Hover/reset retains clip material.
- Folder preview in tests is a simulated visual record, not proof of real Explorer classification/drop.

## Tested object

Base commit `0396093`, dirty tree including local v0.1.8/0.1.9 work. Windows x64 / .NET 10, 2026-10-12.
Final EXE: `artifacts/Snapline-0.1.10-win-x64/Snapline.exe`.
EXE SHA256: `4971E6A42532C88A23B5786AEB21393A1B3ED2BBCB1851A914E541648AD4F715`.
ZIP SHA256: `DFDCA526A055094939CAF6E0C488D242FD89DDEE6DB7FF9672E9A2B34C53EDBA`.

| Layer | Status | Evidence |
|---|---|---|
| Core | PASS, 70 checks | policies for client-hit/header bounds/held source/follow translation/resize exclusion; theme separation |
| Publish | PASS, zero warnings/errors | Build.ps1 -OutputDirectory artifacts/Snapline-0.1.10-win-x64 |
| Final EXE synthetic motion/native-window/style | PASS, 97 checks | design/typed-hanging-v0.1.10.json; artifacts/test-runs/20261012-132016-motion/ |
| E2E harness compilation | PASS, zero warnings/errors | dotnet build tests/Snapline.E2E -c Release |
| Real Explorer empty-folder probe | BLOCKED | scripts/Probe-Explorer.ps1 cannot locate unique fixture caption; no other windows inspected |
| Full physical pointer / existing ordinary app | NOT_RUN | User's v0.1.9 app left running |
| Custom titlebars, real Explorer drop, multi-DPI | NOT_RUN | Must not infer full compatibility from synthetic pass |

Generated preview `design/typed-hanging-preview.png` renders only synthetic title/icon/cards.
No private file lists, existing Explorer paths, mail/browser data, clipboard, desktop screenshots or physical input
were accessed. Probe launched only a new empty fixture folder; unidentifiable tabs were not force-closed, and
explorer.exe was never killed. The current Snapline binary was not replaced, stopped or restarted.

An intermediate synthetic fallback check observed the reference before asynchronous minimize settled;
fixed the test to await both iconic state and completion, not loosen source validation. Failed report remains
local at `artifacts/test-runs/20261012-131550-motion/`. Subsequent development and final package checks passed.
No commit/push/Release publication in this task.
