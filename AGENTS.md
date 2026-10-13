# Snapline contributor / agent instructions

## One feature, one PR

- Every feature must be developed on its own `feat/<scope>` branch and merged through a GitHub pull request into `main`.
- Fixes, documentation and agent-instruction changes also use scoped `fix/…` or `chore/…` branches/PRs. Never push implementation directly to `main`.
- Split unrelated features into separate PRs. Keep each PR independently buildable/testable; land prerequisites first and document dependencies.
- Do not merge unrelated or pre-existing changes without checking their ownership/scope. Preserve a working copy before splitting existing work.
- PR description must state purpose, behavior, exact tests/results and unsupported/unverified scenarios. Synthetic tests are not proof of full physical E2E.
- Wait for required CI checks. Review the diff, then use a normal merge commit (no bypass/admin override) so the PR boundary remains visible.
- Creating/merging PRs, pushing and publishing still require user authorization. An authorized feature request alone is not blanket permission for future releases.
- Release only from merged `main` / an exact tagged commit. Build and verify the actual release EXE; do not substitute an older binary or dirty-build evidence.

## Release automation

- Follow `docs/RELEASING.md`. Release workflow supports tags `v*` and manual main dispatch with an existing tag; version must match the project.
- Tag only merged main commits; never rewrite a published tag or overwrite existing Release assets to get around validation.
- Pipeline/script/version-note changes use their own scoped PR. Publish only with user authorization.
- Use GitHub's scoped workflow token, never a new PAT or credential migration. Build has read-only contents permissions; publish alone requests write.
- Core/build/synthetic startup results are not physical E2E. Record any extra motion/interaction verification against the actual published EXE separately.

## Testing and evidence

- For a feature/fix/test task, read `.pi/skills/snapline-test/SKILL.md` and use its layered workflow/result template.
- Use generated files and synthetic windows. Do not read existing clipboard contents, user image folders, private Explorer windows, credentials or browser data.
- Physical keyboard/mouse/clipboard E2E needs an idle desktop and approval of side effects. Do not kill the user's running app, block their input or bypass security to get tests through.
- Explorer tests use a new empty directory and a uniquely identified fixture window. If identification fails, report BLOCKED; never enumerate private folder paths or kill `explorer.exe`.
- Preserve failed as well as successful reports; use fresh `artifacts/test-runs/…` directories. Mark PASS / FAIL / BLOCKED / NOT_RUN separately for core, isolated UI and physical E2E.
- Never claim File Explorer/custom-titlebar, cross-app drag/drop or mixed-DPI support is fully verified from simulated data alone.

## Runtime / product boundaries

- Preserve transparent surroundings, rope/pegs, independent movable component positions and user-controlled hiding.
- Software-window stashing commits only on release over the rope. Holding/hovering, resize, move-away or Escape must not minimize a window.
- Restore the same living window instance. Keep taskbar recovery available; never close/kill/relaunch an app or persist HWNDs/title/content.
- No account/network/analytics in the app. No Windows animation/security-setting changes; respect reduced motion unless the user explicitly enables the per-app override.
- Keep the MIT/upstream notices and independent Snapline name/artwork. Do not imply upstream endorsement.

## GitHub / identity

- Repository: `https://github.com/ThousandOfWind/snapline-windows`; use HTTPS and the **ThousandOfWind** GitHub account.
- Commit identity: `Julie Zhu (M365) (from Dev Box)` / `zhiruizhu@microsoft.com`, independent of GitHub login.
- Never read/print/migrate tokens, OAuth files, cookies, credential stores or private keys. Stop on authentication, MFA, policy or permission failures.
