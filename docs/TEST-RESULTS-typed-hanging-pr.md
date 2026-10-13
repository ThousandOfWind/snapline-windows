# Typed hanging-materials feature PR validation

Depends on merged window-pocket PR #2 (`deb185c` main merge). This PR changes presentation and
appearance tests only: Image / Window / FolderWindow map to independent frame/header/ink/clip palettes,
wood / metal / brass silhouettes, material-preserving hover/reset and reception preview. The kind enum
was prerequisite classification infrastructure; this PR implements its material theme.

Windows x64 / .NET 10, 2026-10-13.

- Core PASS: 70 checks, including 3 theme-specific checks.
- Development WPF build PASS: zero warnings/errors.
- Isolated native-window/motion/style verification PASS: 97 checks.
- Evidence: `docs/design/typed-hanging-pr.json`; local `artifacts/test-runs/20261013-084454-motion/`.
- Physical input E2E NOT_RUN/BLOCKED: existing ordinary Snapline instance left running.
- Real Explorer drag/drop remains unverified; folder-style fixture is simulated and must not be used as compatibility proof.
- Multi-DPI/custom-titlebar and cross-app interaction remain manual acceptance scenarios.

No user window/file-list inspection, clipboard reading, actual desktop screenshot, input injection,
forced stop or security bypass. Historical local v0.1.10 report retained with its original binary hash;
release must be rebuilt and verified from merged main, not copied from the previous dirty build.
