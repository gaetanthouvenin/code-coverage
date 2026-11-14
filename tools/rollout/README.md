# Rollout tools

Scripts that drive the migration of repositories from Codecov to a CodeCoverage instance. Every
script is a **dry run by default** (add `-Execute`), reads its tokens from environment variables
only, and never prints them. `Test-Rollout.ps1` runs them offline against a local listener with
synthetic values; CI runs it on every pull request.

| Script | What it does |
|---|---|
| `Add-CoverageUpload.ps1` | Opens, through `gh`, a one-step pull request per repository that adds the composite action of CodeCoverage right after the existing `codecov/codecov-action` step (the double upload of the pilot, M4.07). The credential: the `token:` line of the Codecov step when it has one (the imported `CODECOV_TOKEN`); otherwise OIDC when the job grants `id-token: write` (or the workflow does and the job sets no permissions), comments ignored; otherwise, when unsure, the `CODECOV_TOKEN` secret. `-Sha256` (the sum of `coverage-upload-linux-x64`) is written only in a job that plainly runs on an ubuntu x64 runner without a container; elsewhere the action checks the `.sha256` of the release alone. A textual insertion: the rest of the workflow is left byte for byte, a workflow without Codecov or already changed is left alone. A repository without `.github/workflows` is unchanged, an open pull request of an earlier run is kept, a leftover branch fails that repository only; the batch goes on and exits with 1 when one failed. `-Repository owner/name` (repeatable) or `-RepositoriesFile`, `-ActionRepository`, `-Version` and `-Sha256` of the uploader release, `-Url` of the Api. Uses the `gh` of the machine, signed in with an account that may push a branch to the repositories. |
| `Import-CodecovTokens.ps1` | Imports the upload tokens of Codecov (`POST /api/v1/orgs/{forge}/{org}/upload-tokens` and `POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens`, M3.11), so that the pipelines keep their `CODECOV_TOKEN`. The values come from a CSV kept out of any repository (`scope,owner,repo,label,token`); a 409 counts as already imported; no value is ever printed. Needs `COVERAGE_API_TOKEN` (a `cvp_` token of an administrator, in one organization). |
| `Start-CodecovImport.ps1` | Starts the import of the Codecov history of repositories (`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import`, M5.06). Needs `COVERAGE_API_TOKEN` (a `cvp_` token of an administrator of the repositories) and `CODECOV_API_TOKEN`. The instance keeps the Codecov token protected until the import ends, seven days at most. Run it before Codecov is cancelled. |

```bash
pwsh tools/rollout/Test-Rollout.ps1
```

The self-test replaces `gh` with `samples/gh-shim.ps1` (through `ROLLOUT_GH`), which answers from the
frozen workflows of `samples/workflows/` and records the calls; the expected results sit next to them
(`*.expected-add.yml`).

The pilot also counts the repositories that keep `codecov/codecov-action` after the migration: that
count decides the shim of `codecov-cli` (M5.07, ARCHITECTURE §13.2-1).
