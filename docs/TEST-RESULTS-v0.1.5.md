# v0.1.5 — visible hover response and explicit animation option

## Cause and scope

Windows `ClientAreaAnimation` is false on the test computer; the app's ReduceMotion is false.
Previous builds respect the system flag, so spatial animations did not play. The card's old hover
also only straightened its existing tilt, producing zero motion for a zero-angle card. Buttons
changed color immediately; the collapsed entry had no hover gesture.

Fix: explicit per-app opt-in (`启用动画（即使系统已关闭）`), still off by default; ReduceMotion
always takes priority. No Windows setting is changed. Cards swing by 3.5 degrees and scale 1.035;
buttons lift 2 DIPs and scale 1.04; the collapsed photo rotates 4 degrees. Leave/drag/reduce-motion
resets geometry. Button visual children animate, not their input hit targets or saved window positions.

## Build and evidence

- Base commit `c2a2fd1c7d77cbcfa598836a82f03be3651985e6`, dirty working tree with approved design + hover changes.
- Windows x64, .NET 10, 2026-10-12.
- EXE `artifacts/Snapline-0.1.5-win-x64/Snapline.exe`, version 0.1.5.
- EXE SHA256 `866999BEB388F72422C811DC74DA851BDEDB76B0680E4D803254309E7F3A9859`.
- ZIP SHA256 `1F11B2E149CAED86576CD5BCDF8C9C4D539231057E7AA1E833BD90CC85A85EBC`.
- Core: PASS, 36 checks via Build.ps1; publish PASS, zero warnings/errors.
- Final EXE motion/render: PASS, 32 checks via `scripts/Test-Motion.ps1 -Executable <EXE>`.
- Evidence: `design/motion-v0.1.5.json`; local unique run `artifacts/test-runs/20261012-000123-motion/`.
- Full physical E2E / standard smoke: BLOCKED by existing ordinary v0.1.4 instance; not stopped.
- Multi-DPI hover hit testing, actual mouse/drag ergonomics and real capture: NOT_RUN.

## Validation correction

Before explicit test override, the added zero-angle-hover regression appeared to pass because
its conditional assertion skipped when system animation was off. That was inadequate evidence.
The updated verifier explicitly enables per-app animation, asserts intermediate/final animation states,
exercises mouse-enter/leave event bindings on actual buttons/entry, checks return-to-rest and rapid
interruption, and separately validates default system-following and ReduceMotion precedence.
Old v0.1.4 reports prove rendering/no-animation paths, not every spatial animation timeline.

No clipboard access, input injection, real desktop capture or ordinary process termination occurred.
New package is separate; the user's current v0.1.4 EXE was not overwritten. No push/Release performed.
