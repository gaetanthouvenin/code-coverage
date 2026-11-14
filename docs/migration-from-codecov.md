# Migrating a repository from Codecov

This guide moves the pipelines of a repository from Codecov to a CodeCoverage instance. It assumes the
organization is already onboarded (the GitHub App installed and the organization admitted, see
[the organization onboarding guide](operations/organization-onboarding.md)). The `codecov.yml` of
the repository stays as it is: CodeCoverage reads it (`codecov.yml`, `.codecov.yml`, `.github/` and
`dev/` variants) with the same semantics.

In the commands below, `https://coverage-api.example.com` is the public address of the Api of your
instance and `https://coverage.example.com` the one of the Web console (two host names, ADR-011).

## 1. During the pilot: upload to both services

Keep the Codecov step and add a CodeCoverage step next to it, so that both services see every commit.
CodeCoverage publishes its statuses as `coverage/project` and `coverage/patch` while your organization
keeps the default prefix `coverage`: they never collide with `codecov/*`. `tools/rollout/Add-CoverageUpload.ps1`
opens these pull requests for you.

### GitHub Actions (OIDC, no secret)

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write          # the upload authenticates with the OIDC token of the job
    steps:
      - uses: actions/checkout@v6          # a shallow clone is enough: the diff comes from GitHub
      # ... build and test ...
      - uses: codecov/codecov-action@v5    # kept during the pilot
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
      - uses: <owner>/code-coverage/action@v1
        if: ${{ !cancelled() }}
        with:
          version: 1.2.3                   # a release of CodeCoverage
          sha256: <SHA-256 of coverage-upload-linux-x64 of that release>
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

The action downloads the uploader of the pinned release, checks its SHA-256, and never fails the build
(a warning, exit code 0) unless `fail-on-error: true`. See [action/README.md](../action/README.md).

Without the action, the same in a plain step (the binary of the release, its sum checked):

```yaml
      - name: Upload coverage to CodeCoverage
        if: ${{ !cancelled() }}
        env:
          VERSION: 1.2.3
          SHA256: <SHA-256 of coverage-upload-linux-x64>
          COVERAGE_URL: https://coverage-api.example.com
          COVERAGE_FLAGS: unit
        run: |
          curl -fsSL -o coverage-upload "https://github.com/<owner>/code-coverage/releases/download/v$VERSION/coverage-upload-linux-x64"
          echo "$SHA256  coverage-upload" | sha256sum -c - && chmod +x coverage-upload && ./coverage-upload
```

### Azure Pipelines

```yaml
resources:
  repositories:
    - repository: coverage
      type: github
      name: <owner>/code-coverage
      endpoint: <GitHub service connection>
      ref: refs/tags/v1.2.3

variables:
  - group: coverage            # COVERAGE_TOKEN (secret), UPLOADER_SHA256_LINUX_X64...

steps:
  # ... build and test ...
  - template: pipelines/templates/coverage-upload.yml@coverage
    parameters:
      version: 1.2.3
      url: https://coverage-api.example.com
      files: $(Agent.TempDirectory)/**/coverage.cobertura.xml
      flags: unit
```

The template maps `COVERAGE_TOKEN` explicitly (Azure Pipelines never passes a secret implicitly), checks
the sum of the release and the one pinned in the variable group (`UPLOADER_SHA256_{RID}`), and warns
instead of failing. Azure Pipelines builds of a repository hosted on GitHub are supported; repositories
hosted on Azure Repos are not, before the Azure DevOps forge (ADR-014, M7). See
[pipelines/demo/azure-pipelines.yml](../pipelines/demo/azure-pipelines.yml).

### Anything else: curl

```bash
git ls-files > network.txt
curl -fsS --retry 5 -X POST "https://coverage-api.example.com/api/v1/uploads" \
  -H "Authorization: Bearer $COVERAGE_TOKEN" \
  -F slug=my-org/my-repo -F sha="$COMMIT_SHA" -F branch="$BRANCH" -F pr="$PR_NUMBER" \
  -F flags=frontend -F network=@network.txt -F file=@coverage/lcov.info || true
```

### The dotnet tool

On an agent with .NET 10: `dotnet tool install --global CodeCoverage.Uploader`, then `coverage-upload`.
Every option falls back on a `COVERAGE_*` variable (`--flags` on `COVERAGE_FLAGS`, `--file` on
`COVERAGE_FILE`, files separated by `;`).

## 2. Tokens

- **OIDC (recommended on GitHub Actions):** nothing to store. The token of the job proves the
  repository; the instance trusts the numeric account id of the admitted organization.
- **Upload tokens:** keep your `CODECOV_TOKEN` values. An organization administrator imports them, and
  pipelines keep their secret unchanged (the uploader reads `COVERAGE_TOKEN`, then `CODECOV_TOKEN`):
  - console: `/orgs/{forge}/{org}/upload-tokens` (organization token) or the settings of a repository;
  - API: `POST /api/v1/orgs/{forge}/{org}/upload-tokens` or `POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens`
    with `{"label": "...", "importedValue": "<CODECOV_TOKEN>"}` and a `cvp_` API token of an administrator;
  - script: `tools/rollout/Import-CodecovTokens.ps1` (dry run by default, reads a CSV kept out of the
    repository).
  A value can be imported once per organization; a second import answers 409.
- **Badges of private repositories:** import the "graphing token" of Codecov as the badge token, so that
  the badge URLs of your READMEs only change their host: `PUT /api/v1/repos/{forge}/{owner}/{repo}/badge-token`
  with `{"value": "<graphing token>"}`, or the settings of the repository in the console.

## 3. Monorepos and several uploads per commit

- `after_n_builds` per flag (`flags.<name>.after_n_builds`) works as in Codecov. A global
  `codecov.notify.after_n_builds` also works, but counts every upload of the commit: with jobs that
  upload different flags, prefer the per-flag form.
- When the last job knows every upload is done, it adds `--finalize` to its own upload: the uploader
  sends its reports, then declares the uploads of the commit complete, which notifies at once, ignoring
  `after_n_builds` and the debounce. `--finalize` acts only after an upload of the same run; a job
  without a report calls `POST /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete`
  with the same credential as the uploads. It is the counterpart of Codecov's `upload-complete`.
- `manual_trigger: true` waits for that call.

## 4. Badges

Change the host of the badge URLs, nothing else: `https://codecov.io/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`
becomes `https://coverage-api.example.com/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`.
The `github` service segment and the `graphs` variant are accepted too. The Web console gives the
Markdown and HTML snippets of each repository.

## 5. History

Before cancelling Codecov, import the totals Codecov keeps for each commit, so that the history chart
goes back before the migration: `tools/rollout/Start-CodecovImport.ps1` calls
`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import` with a Codecov API token, which the instance
keeps protected until the import ends (seven days at most). The import reads the API of Codecov, which
the cancellation closes: run it first.

## 6. The switch

When the parity of the pilot is explained (`tools/parity`, [parity-log.md](parity-log.md)), an
organization administrator switches the organization: Codecov's notifications stopped, then
`Notifications:StatusContextPrefix` set to `codecov` (and `Notifications:ChecksEnabled` turned on) in the
settings of the organization. The statuses keep the names your branch protections require. The full
sequence is in the cut-over runbook (`docs/cutover-runbook.md`, M4.08).

## Differences with Codecov

Every difference is named here. Those of the isolation between organizations (ADR-013) and of the
multi-forge identity (ADR-014) are not configurable by design; the others are.

| Difference | Configurable | Why |
|---|---|---|
| The App must be installed and the organization admitted before uploads are accepted (401 `Upload.TokenInvalid` for a token, `Repository.NotFound` for OIDC; the uploader still exits 0). | No (ADR-013) | Admission is closed. |
| No global upload token: a token only works in the organization that created or imported it. | No (ADR-013) | Isolation. |
| A transferred repository starts with an empty history, no upload token and a new badge token. | No (ADR-013) | The old history stays with the old organization. |
| After a rename of the GitHub account, the old owner no longer resolves (badges say `unknown`); no redirection. | No (ADR-013) | A reused login must never reach the old organization. |
| The management API and the Web pages carry the forge in their paths (`/api/v1/repos/github/...`, `/github/my-org/my-repo`); the upload API and the badge URLs keep Codecov's forms. | No (ADR-014) | Several forges share one instance. |
| Statuses are named `coverage/*` until the organization sets `Notifications:StatusContextPrefix` to `codecov`. | Yes, per organization | Pilot and Codecov run side by side. |
| Statuses, not Check Runs, until the organization turns `Notifications:ChecksEnabled` on (after accepting the Checks permission of the App); then Check Runs with the same names and patch annotations, as Codecov. | Yes, per organization | Each organization grants the permission at its own pace. |
| Defaults of a repository without `codecov.yml`: the historical profile (project and patch statuses), not Codecov's patch-centred defaults of 2024. | Yes (`codecov.yml`, or the YAML of the organization) | ADR-009, PLAN §11-7. |
| Imported history: commits imported from Codecov have totals only (no files, no sessions); their pages show no file, only the history chart shows them, and they are never compared. | No (the API v2 gives totals only) | M5.06. |
| `comment.behavior: spammy` and `new` may post identical comments more often: CodeCoverage notifies again when a late base report arrives, on the sweep and on a manual re-notify. | No | Freshness over silence. |
| `comment.behavior: new`: when GitHub refuses to delete the earlier comment (403, 422), CodeCoverage logs it and posts the new comment anyway, so the old one may stay beside it. Codecov probably stops instead (`no_permissions`); this is not proven. | No | Kept until the behaviour of Codecov is checked (ARCHITECTURE §8.4). |
| `codecov/codecov-action` and codecov-cli do not talk to CodeCoverage (the compatibility shim is postponed until the pilot measures the need). | — | M5.07. |
