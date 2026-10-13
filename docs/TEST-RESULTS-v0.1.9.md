# v0.1.9 — whole-rope reception, release-only commit

User clarification supersedes the earlier dwell design: approaching the rope previews acceptance;
**release directly hangs the window**. Holding over the rope never auto-stashes or minimizes.
No artificial move-loop cancellation, auto-commit timer or injected mouse-up is used.

## Implementation

- A supported titlebar move reveals the rope automatically; no hidden entry/Logo hunt.
- Whole visible rope is the target, within +/-22 DIP of its curve; decorative sag does not alter hit geometry.
- Near rope: warm highlight, small spring sag, empty peg at approach position and `松手挂上窗口` label.
- Move-away/cancel clears preview. Native move-end is accepted only with mouse released, eligible identity,
  changed position, title region at cursor and explicit reception target. No dwell requirement.
- Existing session-only pocket/minimize/restore/identity/permission guarantees from v0.1.8 are retained.

## Evidence

Base commit `0396093`, dirty tree including local v0.1.8 feature. Windows x64, .NET 10, 2026-10-12.

| Layer | Result | Evidence |
|---|---|---|
| Core | PASS, 56 checks | Removed two obsolete dwell-time checks; release/cancel/source guards remain |
| Publish | PASS, zero warnings/errors | Build.ps1 -OutputDirectory artifacts/Snapline-0.1.9-win-x64 |
| Final-EXE synthetic window/motion/render | PASS, 87 checks | design/window-rope-v0.1.9.json; artifacts/test-runs/20261012-124830-motion/ |
| Full external-app physical E2E / ordinary smoke | BLOCKED | User's v0.1.8 left running; no input/clipboard intervention |
| Real custom titlebar, Snap, multiple DPI/screens | NOT_RUN | Manual acceptance checklist |

EXE `artifacts/Snapline-0.1.9-win-x64/Snapline.exe`.
EXE SHA256 `E9367C96A44FCCA176C7FF29EF34E3259A16E7ED8A34E45007217E6117D1B411`.
ZIP SHA256 `0ACBAFB4872053BB350CF1C1B6A6ABC16C05A8D0428431A82A98D422D1891CA3`.

New checks: left/center/right rope target geometry, immediate empty-peg feedback, off-rope rejection,
reduced-motion visible feedback, approach readiness without minimize, direct release without waiting,
>900ms held-hover non-commit and mouse-held move-end rejection. Synthetic fixtures retain prior
restore/state/permission/handle-lifetime tests. Preview image renders only generated cards, not desktop content.

No auto-hang implementation was packaged: a briefly introduced unused policy proposal was removed
as soon as the user clarified release-only. No user app window tests, forced stops, screenshots,
clipboard access, physical input, system policy changes, commit/push or Release publication.
