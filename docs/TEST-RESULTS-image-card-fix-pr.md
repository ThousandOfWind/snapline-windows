# Image-card interaction and no-fill fix PR

Base main `3bfec25`, branch `fix/image-card-interactions`, Windows x64/.NET 10.0.402, 2026-10-14.
User reported double-click unreliability and excessive screenshot padding. Physical report was not
independently reproduced using injected real mouse input; previous code depended on WPF ClickCount,
opened preview on second press and shared capture with drag. The fix removes those dependencies.

## Behavior

- Preview-phase pointer handlers ignore child buttons and classify per-card system timing/physical distance.
- Two releases open preview once, after capture release, never while second button is held.
- Pending copy cancelled by second click/drag, capture loss and unload. Physical displacement, not hover transforms.
- Image viewport matches actual pixel aspect ratio. No internal padding, gray inset or filename footer;
  filename remains tooltip/accessibility metadata. A thin semantic border and peg spacing remain.
- Group cover also has no inset/fixed letterbox; footer contains actual expand controls rather than blank padding.
- Preview image has zero margin and image-shaped initial layout. Source bitmap/exported contents not cropped/rewritten.

## Results

- Core PASS: 129 checks, including 15 gesture/aspect-ratio checks.
- WPF/E2E harness compilation PASS: 0 warnings/errors.
- Development isolated generated-window/render/routed-input PASS: 152 checks.
- Evidence: docs/design/image-card-fix-pr.json; local artifacts/test-runs/20261014-221855-motion/.
- Added checks verify preview on second release, no delayed copy, single action, drag/cancel separation,
  real WPF PreviewMouse bindings with synthetic clock/point and native mouse capture disabled, zero
  image padding/margin, exact aspect ratio and no visual filename footer.
- Physical user double-click/capture behavior across window activation/DPI, and cross-app drag, remain NOT_RUN.
- E2E harness click locations updated to accessible image center, no hard-coded filename-footer offset.

No actual clipboard access, physical input injection, private image loading, desktop capture or forced app stop.
Synthetic preview uses only generated PNGs. New Release not authorized by this fix task; publishing later
must rebuild/verify from merged/tagged main. Initial verification build used nonexistent WPF double-click
metric properties; corrected to per-DPI native system metrics, no product build with that error shipped.
