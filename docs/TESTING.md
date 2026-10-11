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

## Manual (use only synthetic / non-sensitive fixtures)

- [ ] Start portable EXE without installed .NET; confirm tray icon, no admin request.
- [ ] Ctrl+Alt+T toggles an empty shelf; hide leaves no click-blocking overlay.
- [ ] Add images from a test directory, single-click copies the full image; paste into Paint.
- [ ] Double-click previews once without copying afterward; Esc closes preview.
- [ ] Drag to a temporary Explorer folder copies a file and preserves the source.
- [ ] X and clear remove records, preserve all files; restart restores remaining cards.
- [ ] Edit a watched fixture with Paint; thumbnail refreshes and no locked-file errors occur.
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
- [ ] Quit releases hotkeys/clipboard listener and leaves no process or tray icon.

Automated smoke success is not evidence that these interactive workflows or all
Windows 10/11 configurations have been manually verified.
