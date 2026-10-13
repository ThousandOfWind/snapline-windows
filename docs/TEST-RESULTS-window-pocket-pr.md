# Window-pocket feature PR validation

Scope: session-only app-window stash/restore; whole-rope approach feedback and release-only commit;
standard/custom title-region and missing-event compatibility; identity/lifetime/permissions safeguards;
taskbar recovery, take-all and opt-out. Typed frame/clip materials are deliberately excluded for a
separate feature PR. Minimal card-kind enum is only classification infrastructure.

Based on main merge `626f3c7` (agent workflow instructions). Windows x64 / .NET 10, 2026-10-13.

- Core PASS: 67 checks, `dotnet run --project tests/Snapline.Tests -c Release`.
- Development WPF build PASS: zero warnings/errors.
- Isolated native-window/motion check PASS: 91 checks; `scripts/Test-Motion.ps1`.
- Evidence: `docs/design/window-pocket-pr.json`; local unique run `artifacts/test-runs/20261013-083912-motion/`.
- Physical E2E: NOT_RUN/BLOCKED because ordinary Snapline remains running; no user input/clipboard interference.
- Real Explorer probe remains BLOCKED: exact fixture-window title could not be located; no private file-list/window-path scan.
- Third-party custom titlebars, actual Explorer drag/drop and mixed-DPI behavior remain unverified.

Synthetic own-process fixtures prove real minimize/restore/unsaved-state/placement behavior and
process-scoped WinEvent routing; they are not proof of external-app physical interaction.
No ordinary process killed, no existing clipboard/content read, no real desktop capture.
Historical v0.1.8/v0.1.9 reports retained as development evidence, not this PR's test count.
