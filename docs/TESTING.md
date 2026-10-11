# Test plan

## Automated

`./scripts/Test.ps1` runs core regression tests and a three-second UI smoke test.
The smoke test uses a generated non-sensitive bitmap, temporary settings and inbox,
opens the shelf, then cleanly shuts down and deletes its temporary directory.
It does not register hotkeys, watch folders, read clipboard contents or capture the desktop.
Quit an already-running Snapline instance before testing.

Core tests cover extension allowlists, path boundaries, deduplication, bounded history,
non-destructive removal/eviction/clear, missing-file pruning, safe clipboard defaults,
settings round trip, atomic replacement and corrupt/null settings recovery.

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
- [ ] Full-screen non-sensitive app hides shelf; normal desktop reveals it again.
- [ ] Hotkey collision has a tray notification and menu operations remain available.
- [ ] Quit releases hotkeys/clipboard listener and leaves no process or tray icon.

Automated smoke success is not evidence that these interactive workflows or all
Windows 10/11 configurations have been manually verified.
