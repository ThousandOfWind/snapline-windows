# v0.1.6 — material-based, finite spring motion

## Design

Preserve the approved rope/pegs/logo/buttons, redesign movement rather than increasing amplitude.
Screenshot hover supplies a pendulum impulse around its peg: swing, reverse, decay, rest even while
hovered. No scale/held-angle zoom. Retargeted springs inherit position and velocity. Buttons lift
slightly; press compresses vertically and release springs back. Collapsed entry leaves rope/peg
fixed and gives its paper photo/count tag separate springs. Drag suspends gestures; reduced motion
and the existing per-app system override remain honored.

## Build / evidence

- Base commit `c2a2fd1c7d77cbcfa598836a82f03be3651985e6`, dirty tree including previous approved design changes.
- Windows x64, .NET 10, 2026-10-12. Current user's v0.1.5 process kept running.
- Final EXE: `artifacts/Snapline-0.1.6-win-x64/Snapline.exe`.
- EXE SHA256: `12D5420EE55C5CB3C5F15191D23894D1B0566E4746D843719F7A03335D1CBA40`.
- ZIP SHA256: `FB52F4F970820B608067E3065489D52D8AC5BA1C0BF004244E3B8A1E9146FE6C`.

| Layer | Status | Evidence |
|---|---|---|
| Core | PASS, 41 checks | Build.ps1 output; spring initial state/momentum, reversal, decay, retarget continuity, invalid input |
| Publish | PASS, 0 warnings/errors | Build.ps1 -OutputDirectory artifacts/Snapline-0.1.6-win-x64 |
| Final EXE motion/render | PASS, 39 checks | design/motion-v0.1.6.json; artifacts/test-runs/20261012-001048-motion/ |
| Full physical E2E / standard smoke | BLOCKED | Ordinary v0.1.5 instance running; no forced exit/input automation |
| Multi-monitor / actual hover ergonomics / real capture | NOT_RUN | Remains manual |

## Test-tool correction and intermediate failures

Early development verification sampled a single wall-clock instant in an occluded WPF window;
this missed the impulse/intermediate fade and failed. Development failure reports are retained
in `artifacts/test-runs/20261012-000656-motion/`, `000737-motion/`, and `000822-motion/`.
The verifier now samples multiple frames over a bounded window and renders only generated test
content to pulse WPF. It tests peak and sign reversal, not a relaxed static assertion. A later
handle check also changed to sample the finite impulse rather than expecting a held hover pose.
Development run 000906 and final packaged run 001048 both passed 39 checks.

New checks distinguish temporal design: reversal, natural rest while hovered, no card scale,
button press/release, separate pendant/count phases, cancellation/drag suppression, reduced motion.
This is in-process event/timeline/rendering verification on a packaged EXE, not proof of full
physical-input interoperability or subjective aesthetic satisfaction.

No clipboard access, physical input injection, real desktop capture, system animation setting
change, running-file overwrite, forced stop, push or release publication occurred.
