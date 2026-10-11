# v0.1.7 — image pickup and cursor-following drag preview

## Scope

- Native file-copy drag now shows a floating photo preview following the cursor.
- Source card fades to a placeholder and hides its peg; drop/cancel/failure restores both.
- Preview is a layered, non-activating, WS_EX_TRANSPARENT window and returns HTTRANSPARENT;
  it preserves native copy/no-drop cursors and cannot become the drop target.
- Preview flips near monitor edges; no lag spring while dragging, for direct manipulation.
- Mouse capture arms the drag before leaving source bounds; click/drag are separated.
- Card list reconstruction is deferred while dragging so watcher refresh cannot destroy the source.
- Native GiveFeedback handler and preview timer/window are disposed in finally paths.

## Evidence

Base commit `c2a2fd1c7d77cbcfa598836a82f03be3651985e6`, dirty tree with preceding approved changes.
Windows x64, .NET 10, 2026-10-12.

- Final EXE `artifacts/Snapline-0.1.7-win-x64/Snapline.exe`.
- EXE SHA256 `752F3CCDB040E6D5DE9D44A2889DBD2EB27A8BEC59AEA5AD6D3B188DCAA763F8`.
- ZIP SHA256 `186915C111323184AA80AB04D3DFC81F142ABEF6F9CBD6AFBDAAF0FBB556F9F9`.
- Core PASS: 41 checks; publish PASS, zero warnings/errors.
- Final-EXE isolated motion/drag-visual checks PASS: 45 checks, exit 0.
- Evidence `design/motion-v0.1.7.json`, local `artifacts/test-runs/20261012-001628-motion/`.
- Added physical native-drag/cancel E2E scenario compiles, but NOT_RUN: existing ordinary v0.1.6
  instance was not stopped and input/clipboard intervention not authorized for this turn.
- Cross-app OLE drop compatibility, actual pointer follow during OLE loop and mixed-DPI dragging
  remain unverified by the isolated checks; must not treat visual tests as full interaction proof.

New isolated checks cover source opacity/peg pickup, preview native input transparency, supplied
position tracking without moving the mouse, monitor-edge flipping, disposal and exception restoration.
No existing clipboard access, physical input injection, desktop capture, forced process termination,
current-EXE overwrite, system setting change, push or release publication occurred.
