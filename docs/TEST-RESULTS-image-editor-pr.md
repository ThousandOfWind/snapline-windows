# Built-in image-editor feature PR validation

Base main `0852cef`, branch `feat/image-editor`, Windows x64 / .NET 10.0.402, 2026-10-14.
Scope: nine local tools (pen/highlight/arrow/rectangle/ellipse/text/crop/mosaic/opaque cover), color/width,
zoom/fit, undo/redo, new flattened PNG export, edited-image copy callback, preview/context-menu entry.
No rotation, output resampling/JPEG compression, vector-layer project format or existing-mark selection/move.

## Results

| Layer | Status | Evidence |
|---|---|---|
| Core | PASS, 114 checks | 25 new editor state/crop/validation/history cases plus previous 89 |
| WPF/E2E harness compilation | PASS, 0 warnings/errors | dotnet build |
| Development isolated generated-data/native/UI/render | PASS, 140 checks | docs/design/image-editor-pr.json; artifacts/test-runs/20261014-103937-motion/ |
| Physical mouse/clipboard/native-dialog E2E | NOT_RUN | No keyboard/mouse/clipboard intervention authorized |
| Large image/DPI/font/codec coverage | NOT_RUN | Manual checklist in TESTING.md |

Generated editor preview: docs/design/image-editor-preview.png. It is a renderer capture of the test
window with generated source/labels, not a real desktop screenshot.

New checks validate pixel changes for each vector tool, crop dimensions and global-coordinate alignment,
opaque alpha, pixel blocks, no uncovering earlier opaque cover via mosaic, decodable new PNG, refusal of
source/existing destination overwrite and original-byte hash preservation. UI checks route copy to a fake
callback (no real clipboard), save default copy, dirty/cancel behavior and preview editor request.
Direct commands are not evidence that physical mouse/touch drawing or native Save As/close confirmation
worked. Snapshot immutability/histories do not claim undo of marks baked into a previously loaded PNG.

## Safety/design

- Source is loaded OnLoad, not held locked. Saves always use CreateNew and never overwrite originals/existing files.
- Source group's members remain unchanged; output is a separate image.
- Output is raster PNG, no embedded original or edit-layer payload. Original file remains on disk, so share the edited copy.
- Mosaic does not guarantee non-inference; use opaque cover for sensitive information. Prior cover is flattened before mosaic.
- Unsaved close confirmation in production; copy alone does not mark disk state saved.
- Busy/modal editor suppresses unrelated show/capture interactions; no external editor installation/process launch.

Initial development renderer verification failed mosaic block consistency. Replaced scaling-based pixelation
with explicit per-block averaging of the already composited raster and tested points in the same cell;
opaque-cover-before-mosaic check remains strict. Failed report retained at local
artifacts/test-runs/20261014-103014-motion/. Subsequent successful runs retained separately.

No private images/clipboard, real desktop capture, user-window operation, input injection, forced process
termination, privilege bypass or system-setting changes. New Release not authorized by this feature task;
any later publication must use the merged/tagged source and validate that exact EXE.
