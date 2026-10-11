# v0.1.4 — approved hanging-photo design

## Test object

- Windows x64, .NET 10, local interactive session, 2026-10-11.
- Base commit: `c2a2fd1c7d77cbcfa598836a82f03be3651985e6`; uncommitted design changes (dirty tree).
- Packaged EXE: `artifacts/Snapline-0.1.4-win-x64/Snapline.exe`, FileVersion `0.1.4.0`.
- EXE SHA256: `CA461246BBCC3BAC13D08C12D1070673B97F0C022B30DCA1111D7BEDC51CE524`.
- ZIP SHA256: `3B522E50873882A9EB28C9176F5CA1EFF6935A5C2FDC1DB6D67C27F0883C349F`.

## Scope

User-approved concept: preserve rope/clothespins; replace abstract logo, collapsed word-button
and heavy controls with a hanging screenshot logo, miniature photo/count entry and paper controls.
Add finite hanging, copy and remove feedback; retain transparent surroundings and saved component positions.

| Layer | Command / object | Result | Evidence |
|---|---|---|---|
| Core | `dotnet run --project tests/Snapline.Tests -c Release` (via Build.ps1) | PASS, 34 checks | build output |
| Publish | `scripts/Build.ps1 -OutputDirectory artifacts/Snapline-0.1.4-win-x64` | PASS, 0 warnings / errors | EXE/ZIP hashes above |
| Isolated final-EXE motion/render | `scripts/Test-Motion.ps1 -Executable artifacts/Snapline-0.1.4-win-x64/Snapline.exe` | PASS, 20 checks, exit 0 | `design/motion-v0.1.4.json`; local `artifacts/test-runs/20261011-235301-motion/` |
| Standard smoke | `scripts/Test.ps1` | BLOCKED: ordinary instance is running | no process killed |
| Full physical-input E2E | `scripts/E2E.ps1` | BLOCKED: ordinary instance is running; clipboard/input authorization required | not executed; no claim of full acceptance |
| Manual multi-monitor / real capture / cross-app | — | NOT_RUN | remains in TESTING.md |

## New coverage

- Entry intermediate/final animation states; interruption never leaves opacity/translation stuck.
- Reduced-motion immediate entry/exit; existing settings load new option safely and persist it.
- New screenshots settle with stable peg-relative positions; rope and both pegs remain present.
- Copy response settles; cancellation of card animation restores the stable pose.
- Shelf mostly transparent (>65% pixels in the synthetic two-card fixture).
- Rapid actual shelf conceal/reveal, hide then recall, clearing motion before component movement.
- Animated removal leaves source image untouched and shelf accessible.
- Shared logo resource and separate collapsed count tab load/render.
- Test verifies timelines/rendering in the actual packaged app, not full end-to-end physical interaction.

## Safety / delivery

All checks use generated PNGs and isolated temporary data. No existing clipboard read/write,
input injection, real desktop capture, user image import or forced app stop. Existing ordinary
v0.1.3 application remained running and was not overwritten. New package is in a separate directory.
No push or Release publication performed for this design update.

The earlier unapproved capture-frame/rope-removal design was superseded by the approved
hanging-photo design, never applied to the running app. Static concept art is not test evidence.
