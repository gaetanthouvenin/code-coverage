# Rollout tools

Scripts that drive the migration of repositories from Codecov to a CodeCoverage instance. Every
script is a **dry run by default** (add `-Execute`), reads its tokens from environment variables
only, and never prints them. `Test-Rollout.ps1` runs them offline against a local listener with
synthetic values; CI runs it on every pull request.

| Script | What it does |
|---|---|
| `Start-CodecovImport.ps1` | Starts the import of the Codecov history of repositories (`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import`, M5.06). Needs `COVERAGE_API_TOKEN` (a `cvp_` token of an administrator of the repositories) and `CODECOV_API_TOKEN`. The instance keeps the Codecov token protected until the import ends, seven days at most. Run it before Codecov is cancelled. |

```bash
pwsh tools/rollout/Test-Rollout.ps1
```
