# Screenshot-group feature validation

Based on main `6d72310`; feature branch `feat/screenshot-groups`. Windows x64 / .NET 10, 2026-10-13.
One feature: in-app image/group drop creation, append/merge, stacked cover/inline expansion, all-member
file-copy/drag payload, ungroup/member take-out and persistent membership. App-window cards are not group members.

## Scope and safety

- Grouping changes references only; library capacity still counts all image files, no file deletion/move.
- Every path has one group owner, groups are flat, target ID persists on merge; singleton groups dissolve.
- Backward-compatible Preferences.Groups, corrupt/missing/duplicate member repair and atomic invalid-drop refusal.
- Standard FileDrop array for other apps; bounded session-tagged in-app selection, no external drop auto-import.
- Group copy is multiple file references, not a cover bitmap or stitched long image; target app must support multi-file input.
- Source drop hit target retained to reject self-drop rather than exposing a window underneath. Drag preview remains input-transparent.
- Physical cursor displacement separates image drag from hover oscillation and component repositioning.

## Results

- Core PASS: 89 checks, including 19 new model/persistence/non-destructive tests.
- Development build PASS: 0 warnings/errors.
- Isolated generated-image/native-window/motion/group-control checks PASS: 117 checks.
- Evidence: `docs/design/screenshot-groups-pr.json`; unique run `artifacts/test-runs/20261013-114510-motion/`.
- Synthetic previews: `docs/design/groups-collapsed-preview.png`, `groups-expanded-preview.png`.
- Full physical OLE grouping/drop, whole-group paste into Explorer/chat apps, restart UI and multi-DPI: NOT_RUN/BLOCKED
  while ordinary Snapline remains running and physical input/clipboard side effects have not been authorized.
- No existing clipboard read/write, real desktop capture, user-image import or user-process termination in verification.

UI verification exercises group command/payload validation, actual expand button routing, collapsed/expanded rendering,
persistence commands, all-member FileDrop contents, move-out/ungroup and source hit preservation. This is not proof of
physical Drop event receipt or another app successfully receiving all files. Existing Explorer/window-stash limitations remain.

Initial verification helper build failed nullable checks on synthetic IDataObject results; explicit non-null fixture
assertions fixed the helper. Earlier reports retained separately; counts above are from the current successful run.
No new Release authorized/published by this feature task. Any later release must be rebuilt and reverified from merged main.
