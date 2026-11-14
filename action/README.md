# CodeCoverage upload action

A composite action that downloads the standalone CodeCoverage uploader of a **pinned release**,
checks its SHA-256, and uploads coverage reports to a CodeCoverage instance. Bash only, no
third-party action; every input reaches the scripts through `env:`.

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write   # the upload authenticates with the OIDC token of GitHub Actions
    steps:
      - uses: actions/checkout@v6
      # ... build and test, producing coverage.cobertura.xml ...
      - uses: <owner>/code-coverage/action@v1
        with:
          version: 1.2.3
          sha256: <SHA-256 of coverage-upload-linux-x64 of v1.2.3>   # optional, recommended
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

## Inputs

| Input | Required | Default | Meaning |
|---|---|---|---|
| `version` | yes | | The release of the uploader, without the `v` (`1.2.3` for the tag `v1.2.3`). |
| `sha256` | no | | The SHA-256 the binary of the runner must have. The `.sha256` of the release is always checked; a pinned sum also protects against a replaced release. |
| `url` | yes | | The public address of the CodeCoverage Api. |
| `token` | no | | An upload token (`CODECOV_TOKEN` keeps working once imported). Empty: OIDC, which needs `id-token: write`. |
| `files` | no | | Report files, separated by `;` or new lines. Empty: the workspace is searched. |
| `flags` | no | | Flags, separated by commas. |
| `name` | no | | The name of the upload. |
| `dry-run` | no | `false` | Prepares and prints the upload without sending it. |
| `fail-on-error` | no | `false` | Fails the step when the installation or the upload fails. |
| `download-url` | no | `https://github.com/<repository of the action>/releases/download` | Where the release files are read. The repository of the action comes from `github.action_repository`, or from the checkout path of the action when that is empty. |

Output `installed`: `true` when the uploader was downloaded and its sum verified.

## Supported runners

`linux-x64` (glibc), `linux-musl-x64` (Alpine containers), `win-x64` and `osx-arm64`. Another runner
gets a warning and no upload.

## Failures never break the build

A failed download, a sum that does not match, or an unsupported runner writes a `::warning::` naming
the cause and ends the step with exit code 0; the upload step is skipped. The uploader itself exits
with 0 on an upload failure too. Set `fail-on-error: true` to fail instead.

## Where the binary comes from

The binaries and their `.sha256` are those of the GitHub release of the pinned version, extracted
from the Api image of the same tag (ADR-012, amended for M5.01): the version and the sum change
together, when the workflow is updated. The route `/api/v1/uploader/{rid}` of an instance stays
available as a fallback and for manual use, but it serves the version of the server, so a sum pinned
in a workflow would stop matching at the next server upgrade.

## Differences with `codecov/codecov-action`

- The binary is the CodeCoverage uploader, not the Codecov CLI; `codecov/codecov-action` keeps working
  only with Codecov (the codecov-cli shim, M5.07, is postponed).
- No `use_oidc` switch: an empty `token` means OIDC.
- `url` is required: there is no default instance.
- The moving tag `v1` is moved by the maintainers to the latest `v1.x.y` release, by hand.
