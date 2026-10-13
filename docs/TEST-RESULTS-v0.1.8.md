# v0.1.8 — session-only software window pocket

## Object and scope

Base commit `0396093`, dirty working tree. Windows x64, .NET 10, 2026-10-12.
User requested deliberate titlebar drag onto Snapline, temporary storage and return to the same app state.

- Move/size WinEvent observer (out-of-context, skips Snapline's process); no global mouse hook.
- Entry/expanded Logo are explicit targets. Require a titlebar-origin move, changed position,
  final title region at pointer, target dwell >=300ms and no observed Escape.
- Only ordinary minimizable top-level windows; desktop/taskbar/tool/owned/dialog/self excluded.
- Pocket has separate 20-window capacity; full capacity rejects, does not evict a living window.
- Stash records original placement, icon/title and session identity, then minimizes.
- Unique lifetime window property plus process identity protects against same-process HWND reuse.
- Click window card restores original instance, placement/max state. Foreground failure is reported;
  no injected keys, AttachThreadInput, privilege elevation or security bypass.
- Take-all, opt-out, close/prune and taskbar recovery. No live content screenshots or persisted HWND/title.
- Taskbar/Alt+Tab remains recovery path even if Snapline crashes or exits.

## Results

| Layer | Result | Evidence |
|---|---|---|
| Core | PASS, 58 checks | Build.ps1: identity/capacity/drop guard tests plus prior image/settings/spring regressions |
| Publish | PASS, 0 warnings/errors | Build.ps1 -OutputDirectory artifacts/Snapline-0.1.8-win-x64 |
| Final-EXE isolated native-window + motion/render | PASS, 77 checks | design/window-pocket-v0.1.8.json; artifacts/test-runs/20261012-102042-motion/ |
| Existing E2E suite build | PASS, 0 warnings/errors | dotnet build tests/Snapline.E2E -c Release |
| Full physical external-app E2E / normal smoke | BLOCKED | User's v0.1.7 process left running; no keyboard/mouse/clipboard intervention |
| Browser/custom titlebar, Snap, DPI, admin/fullscreen | NOT_RUN | Manual checklist in TESTING.md |

EXE: `artifacts/Snapline-0.1.8-win-x64/Snapline.exe`.
EXE SHA256: `87EA9B698E6AC623C68D27F7BA85C887C7FEA5280998E47263BB7E88BF6E95B4`.
ZIP SHA256: `719CCDF09EC8287BA90703348794DD17D81268B0CEFE5774C8337DC9535F00AE`.

## Verification details and failures fixed

Fixtures are newly generated own-process WPF windows with synthetic unsaved textbox data.
Tests use actual minimize/restore/placement APIs, actual window-card Click routing, and process-scoped
WinEvent callbacks (only the verifier PID). Gesture endpoint tests supply synthetic cursor coordinates
without physically moving the mouse. They do not prove third-party titlebar events work on every app.

Checks cover unsaved content retention, unaffected second window, duplicate refusal, restore/take-all,
maximized-state/normal-placement recovery, taskbar-style recovery, old/lost lifetime marker rejection,
closed window cleanup, no startup HWND persistence, owned dialog refusal, cancellation and disabled mode.
The first synthetic stash exposed an incorrect minimize-box style constant (MINIMIZE vs MINIMIZEBOX);
fixed after own-window diagnostics. Intermediate failure records retained in local test-runs directories
100957, 101025 and 101119. Subsequent development and final packaged checks passed.

No user software contents inspected, no real desktop capture, no clipboard access, no physical input
injection, no global/external hooks in the verifier. Existing application not overwritten/terminated.
Feature is implemented and packaged locally; no commit, push or Release publication in this task.
