# Release pipeline

Workflow: [Release Windows](https://github.com/ThousandOfWind/snapline-windows/actions/workflows/release.yml).
No PAT or personal credential configuration is needed. Build job has read-only contents access;
publish job uses GitHub's scoped GITHUB_TOKEN with contents:write. It publishes download assets,
not an application deployment.

## Prepare (via PR)

1. Merge each feature/fix PR into main after CI. Review known limitations and test evidence.
2. Set `src/Snapline.Windows/Snapline.Windows.csproj` Version through a PR. It must exactly match the tag without `v`.
3. Add optional `docs/releases/v<version>.md` notes in that PR. If absent, the pipeline uses a short generic note plus verification metadata.
4. Keep actual user-interaction E2E distinct from core/build/synthetic startup checks. Do not add unsupported claims.

## Trigger

After merged main is clean:

```powershell
git switch main
git pull --ff-only origin main
git tag -a v0.1.11 -m 'Snapline 0.1.11'
git push origin v0.1.11
```

Tag pushes trigger the release workflow. Alternatively, Actions → Release Windows → Run workflow,
select **main** and enter an existing tag containing this pipeline (e.g. `v0.1.11`).
Manual dispatch from a feature branch is disabled. Legacy tags created before pipeline scripts existed
are not supported for rebuilding via this workflow; their existing releases remain unchanged.

## Gates and outputs

- Valid `vMAJOR.MINOR.PATCH` tag (optional prerelease suffix) matches project Version.
- Exact checked-out tag commit is already an ancestor of origin/main; clean source checkout.
- Release-helper validation tests, existing core tests, self-contained win-x64 publish.
- Actual EXE version and bounded `--smoke-test` startup/shutdown using generated data only.
- SHA256 checksums and manifest with source commit, core count, smoke scope and NOT_RUN physical E2E.
- Tag commit rechecked before publish. Existing Release notes/assets are left unchanged, never automatically overwritten.
- No force-tag, admin merge bypass, elevation, signing setup or Windows security/animation changes.

Published assets: `Snapline-win-x64.zip`, `SHA256SUMS.txt`, `release-manifest.json`, `startup-report.json`.
PR changes to the pipeline/scripts run the package/validation/smoke jobs without publication.
If authentication/permission or smoke fails, publication stops; diagnose rather than bypass it.

## Extra local acceptance

Download the actual published ZIP, verify its hash, then use `scripts/Test-Motion.ps1 -Executable <downloaded EXE>`
for isolated visual checks. Physical `scripts/E2E.ps1` requires an idle desktop, app exit and approval
of mouse/keyboard/clipboard effects. Attach/report results separately; neither CI nor synthetic smoke
proves cross-app group dragging, Explorer/custom titlebars or mixed-DPI behavior.
