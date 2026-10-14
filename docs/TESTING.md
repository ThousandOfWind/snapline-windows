# Test plan

Reusable project skill: [snapline-test](../.pi/skills/snapline-test/SKILL.md).
Use `/skill:snapline-test <feature or bug>` in Pi opened from this repository.
It defines safety gates, new-feature coverage, layered validation and an evidence template;
this document keeps the concrete coverage and manual acceptance checklist.

## Automated

`./scripts/Test.ps1` runs core regression tests and a three-second UI smoke test.
The smoke test uses a generated non-sensitive bitmap, temporary settings and inbox,
opens the shelf, then cleanly shuts down and deletes its temporary directory.
It does not register hotkeys, watch folders, read clipboard contents or capture the desktop.
Quit an already-running Snapline instance before testing.

Core tests cover extension allowlists, path boundaries, deduplication, bounded history,
non-destructive removal/eviction/clear, missing-file pruning, safe clipboard defaults,
settings round trip, atomic replacement and corrupt/null settings recovery.

## Isolated visual/motion checks

`./scripts/Test-Motion.ps1 [-Executable <EXE>]` uses a separate verification instance and generated images.
It does not register tray/hotkeys/watchers, read the clipboard, inject input, capture the actual desktop,
stop the normal application, or read its settings. Evidence goes to a unique `artifacts/test-runs/*-motion/`.
Checks cover animation intermediate/final values, interrupted transitions, reduced-motion behavior,
new card visibility, actual shelf hide/recall and shared understandable logo/labels.
Drag visual checks additionally verify source dimming/unclipping, input-transparent floating thumbnail,
explicit cursor-position following, screen-edge flipping and dispose/exception restoration.
Window pocket tests use generated own-process WPF windows (including unsaved textbox data), actual
minimize/restore/placement APIs and process-scoped synthetic WinEvent notifications. No global/external
window hooks are installed by verification. They cover card click, max-state/position restore, take-all,
closed/reused-handle protection, owned-dialog rejection, cancellation/release and opt-out.
0.1.9 additionally checks targets across the full rope, empty peg feedback, off-rope cancellation,
readiness without animations, immediate release commit, long held-hover non-commit, and rejection of
move-end while the left button is still held. The prior dwell-to-ready behavior is superseded.
0.1.10 adds HTCLIENT/timeout caption-region policy, child-source normalization, pointer-follow fallback
without move-start/end events, bounded release-state timing, and distinct photo/window/folder frame and clip materials.
Folder style fixture is simulated: do not interpret it as real Explorer interoperability evidence.
These in-process rendering/timeline checks are not an end-to-end user interaction test.
As of 0.1.5, verification explicitly enables the per-app animation override, so intermediate-motion and hover
assertions are not silently skipped when Windows animation is disabled. It separately verifies default
system-following behavior and that ReduceMotion wins over the override. No Windows setting is changed.
0.1.6 adds spring momentum continuity/decay tests, pendulum reversal and natural rest while hovered,
no card scaling, separate photo/count phases, button press/release compression and drag suspension.
Intermediate animation checks sample a time window and explicitly render synthetic frames, avoiding
false failures from a single wall-clock sample while WPF is occluded.
Checks also exercise mouse-enter/leave routed-event bindings on buttons/entry without physical input;
this proves animation/event wiring, not actual mouse hit-testing on every display.

## Interactive desktop E2E (Windows, unlocked interactive session)

`./scripts/E2E.ps1` launches the actual self-contained portable EXE in a separate process.
It uses UI Automation, real keyboard/mouse input and real filesystem/clipboard notifications.
91 checks cover visible startup, second-launch forwarding, hotkeys, watched thumbnail rendering,
full-resolution single-click copy, double-click preview, region drag/cancel, non-destructive removal,
hover/auto-hide, clipboard opt-in/off, self-copy suppression, mixed text/private marker rejection,
external deletion, tray menu exit, and restart persistence.
0.1.2 additionally verifies transparent rendered pixels, transparent-area native hit testing,
auto-restoration and pinning after capture/cancel, >5-second visibility over a generated fullscreen form,
clickable captured thumbnail, rapid hide/reveal animation reset, deliberate pin vs hover-only reveal,
physical recovery-handle clicking, and the toolbar-button capture path (not just hotkey capture). The rendered shelf preview contains only synthetic fixtures.

0.1.3 adds real grip dragging on both axes, compact floating shelf width, independent collapsed-handle dragging
without accidental click/open, hide/reveal and capture-cancel placement retention, independent restart restore,
and real tray reset. Core tests cover normalized/clamped/invalid positions, oversized component travel,
old-settings defaults and independent placement persistence. Test desktop must stay idle: other mouse input
can interfere with physical automation.

The runner uses only a unique temporary inbox/settings directory and generated images.
`--e2e-data-dir <path>` is a test-only startup option that disables default Pictures folders
and supplies a synthetic desktop to the normal region-selection UI; it never captures the real desktop.
Do not use this option for ordinary app use. Existing Snapline instances cause an explicit test failure.
Tests never inspect the existing clipboard; they overwrite it with generated fixtures and clear it afterward.
Run only when this clipboard replacement and brief keyboard/mouse automation are acceptable.
Tray menu opening uses the actual NotifyIcon callback, not a physical click on Explorer's tray icon.
Clipboard format/content assertions read only the fixture the test just copied successfully.
The report is `artifacts/e2e-report.json` (ignored by git, no private paths/content included).

Actual desktop screen capture fidelity, drag-to-other-app interoperability, Save As/painting,
private markers beyond those covered, and multi-monitor/mixed-DPI combinations remain manual.

## Screenshot group regression

Core checks: valid image-on-image creation, unique ownership, group identity/order, append/merge/no nesting,
self/foreign/malformed drop rejection, pruning and singleton dissolution, snapshots, ungroup/file preservation,
old/corrupt settings and persistence round trip.
Isolated GUI checks: session-tagged payload and standard FileDrop, collapsed group stack, expansion control,
member views, append command, all-member copy/drag payload, collapse, take-out and ungroup.
Payload generation is tested without reading/writing the existing clipboard. Command/binding checks do
not prove a physical OLE drop or multi-file receipt in Explorer/chat apps.

## Card interaction / no-fill regression

Core tests classify single/double/drag/cancel without WPF ClickCount; double only commits after the second release,
never leaks delayed copy, and respects system timing/distance. Layout tests preserve landscape/portrait/square aspect.
Isolated binding checks drive the same classifier and actual WPF preview routed-event handlers using synthetic clocks/
points with native mouse capture disabled, and open a generated-image preview. No physical input or clipboard access.
Pixel-view tests require zero padding/margin, exact aspect ratio and no filename footer. Group cover retains action controls
but not image inset/letterbox fill. These checks are not a substitute for physical double-click timing and native capture.

## Image editor regression

Core: immutable marks, pixel crop normalization/clamp, dirty snapshots, undo/redo/branch reset,
invalid coordinates/text/width/tiny shapes and bounded history/annotation limits.
Isolated renderer/UI: all vector tools change pixels, full resolution, crop/mark alignment, opaque cover,
mosaic blocks and no uncovering earlier redaction, new PNG/source-byte preservation, copy callback,
controls, cancel draft, preview edit entry, default save and dirty state. Only generated images; no actual clipboard write.
Direct model/control routing does not prove real mouse/touch drawing, native dialogs or every font/DPI/image format.

## Manual (use only synthetic / non-sensitive fixtures)

- [ ] Start portable EXE without installed .NET; confirm tray icon, no admin request.
- [ ] Hover upright/tilted screenshot cards, capture/fold/remove buttons and collapsed entry; move away and verify rest pose.
- [ ] With Windows animations off: explicit per-app override enables motion; ReduceMotion suppresses it again.
- [ ] Drag collapsed entry while hovered and click paper buttons near edges; no flicker, stuck scale or saved-position drift.
- [ ] Ctrl+Alt+T toggles an empty shelf; hide leaves no click-blocking overlay.
- [ ] Add images from a test directory, single-click copies the full image; paste into Paint.
- [ ] Double-click previews once without copying afterward; Esc closes preview. Test quick/slow clicks, jitter, hover swing, drag on second press, lost capture and different DPI.
- [ ] Wide, tall, square and very thin screenshots fit their own aspect ratio with no gray/white synthetic fill; source contents/pixels not cropped.
- [ ] Group covers have no inset padding; expanded images share the same click/drag behavior; controls/accessibility labels remain available.
- [ ] Drag one Snapline test image onto another: highlight, drop, group stack/count, no external copy or lost image.
- [ ] Drag a new test image / group onto a group; self-drop and Escape do not mutate membership.
- [ ] Expand/collapse group; member copy/preview/export and move-out work; group card still exports whole group.
- [ ] Drag group into an empty test Explorer folder and copy/paste group: all files arrive, originals stay; test chat apps separately.
- [ ] Group persistence after normal restart; missing/deleted member shrinks group and a singleton becomes standalone.
- [ ] Window cards cannot join image groups; external untagged FileDrop cannot mutate library metadata.
- [ ] Drag to a temporary Explorer folder copies a file and preserves the source; floating thumbnail follows cursor without blocking the target.
- [ ] Esc cancels image drag and restores source opacity/peg; original file and card remain.
- [ ] Quickly drag outside card bounds, across monitors and back; no accidental copy click, lost gesture or dangling preview.
- [ ] Default/reduced animation settings both retain drag thumbnail (it is functional feedback, not a decorative animation).
- [ ] X and clear remove records, preserve all files; restart restores remaining cards.
- [ ] Open generated screenshot from right-menu and preview edit button; tools/keyboard shortcuts work without an external app.
- [ ] Pen/highlight/arrow/box/ellipse/text at multiple zoom/DPI levels; reverse crop, tiny crop, Esc and undo/redo align correctly.
- [ ] Copy edited result into a permitted test target and save a new PNG; original hash and old group members unchanged.
- [ ] Save As existing/source filename is refused; unwritable destination fails without altering source or reporting saved.
- [ ] Close with unsaved edits: confirm/discard/cancel behavior; clipboard copy alone is not a saved file.
- [ ] Opaque cover flattens only edited output; do not claim mosaic is guaranteed secret redaction, share only edited copy.
- [ ] Large images, GIF/static-frame and WebP codecs, transparent inputs and system fonts; log limits/unsupported formats.
- [ ] Ctrl+Alt+S selects a synthetic desktop area; reverse drag, tiny selection, Esc cancel.
- [ ] Clipboard is off by default. Opt in, copy synthetic bitmap and see one new card.
- [ ] Copy from shelf while opted in; no duplicate. Copy mixed text/file data; no new card.
- [ ] Test known `ExcludeClipboardContentFromMonitorProcessing` marker; no new card.
- [ ] Add/stop a test watch folder; no historical-file import or recursive folder scanning.
- [ ] Temporarily lock a newly created image while writing, then release; it loads once.
- [ ] Move/delete a fixture externally; stale reference disappears.
- [ ] >50 test images evict oldest references without deleting source files.
- [ ] Hover top edge on each monitor, including negative origins and 100%/150% mixed DPI.
- [ ] Move between monitors, change display settings, resume from sleep.
- [ ] Drag shelf grip and collapsed handle between mixed-DPI/negative-origin monitors; restart with a monitor disconnected.
- [ ] Drag both components to every work-area edge; verify taskbar remains accessible and reset recovers placement.
- [ ] Full-screen non-sensitive app hides shelf; normal desktop reveals it again.
- [ ] Hotkey collision has a tray notification and menu operations remain available.
- [ ] Drag a non-sensitive standard-titlebar test app: rope auto-reveals; approach anywhere along rope shows peg/sag; release commits immediately.
- [ ] Hold over rope for >2 seconds: no auto-stash; move away/release elsewhere/Esc clears preview without minimizing.
- [ ] Restore via window card, taskbar, and take-all; unsaved test document stays intact.
- [ ] Resize, body overlap, move cancelled by Esc, and feature disabled do not minimize another app.
- [ ] Close a stashed test app, restart another app; stale card cannot activate reused HWND.
- [ ] Exit/crash Snapline with a stashed test app: taskbar/Alt+Tab remains an independent recovery path.
- [ ] Test File Explorer/Chrome/Edge/VS Code custom titlebars, maximized/snapped windows, multiple monitors and different DPI; document unsupported cases.
- [ ] File Explorer test must use a newly generated empty directory. Do not inspect the user's existing windows/file lists or enumerate their folder paths.
- [ ] If a fixture window cannot be identified uniquely, mark BLOCKED; never use a generic Explorer title or kill the explorer.exe process.
- [ ] Confirm photo/ordinary window/recognized folder window have distinguishable frame, header and clip silhouettes after hover/reset.
- [ ] Diagnostic popup reports gate state only, no private title/path; no commit while held.
- [ ] Elevated/system/dialog windows rejected, no elevation prompt or foreground-policy bypass.
- [ ] Quit releases hotkeys/clipboard/WinEvent listeners and leaves no process or tray icon.

Automated smoke success is not evidence that these interactive workflows or all
Windows 10/11 configurations have been manually verified.
