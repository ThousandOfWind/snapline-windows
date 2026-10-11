# v0.1.3 validation

- Release development build: 0 warnings / 0 errors.
- Core tests: 32 passed, including normalized/clamped placement, invalid coordinates,
  zero travel, older settings and independent position persistence.
- Development EXE: 91 interactive E2E checks passed in one complete run.
  New coverage: real shelf grip drag on both axes, compact floating width,
  independent handle drag without accidental open, position after hide/reveal,
  capture cancellation at moved position, restart restoration of both components,
  and real tray reset.
- Portable x64 self-contained publish: successful.
- Portable EXE E2E: not a complete pass. One run reached 79 checks, including
  both component drags, independent persistence, hide/reveal and capture cancellation;
  an immediate placement assertion after capture returned before positioning settled.
  That assertion now waits for stable placement. Other runs were interrupted by
  concurrent cursor movement (reported cursor moved away from injected target).
  Do not treat those partial runs as full acceptance evidence.
- Multi-monitor/mixed-DPI drags, disconnected-monitor recovery and third-party
  image file drag/drop remain manual tests.

E2E runs use only temporary settings/inbox, generated clipboard fixtures and a
synthetic capture background; they do not inspect the existing clipboard or
capture the real desktop. Run `scripts/E2E.ps1` on an idle interactive Windows
session to reproduce the full test and get `artifacts/e2e-report.json`.
