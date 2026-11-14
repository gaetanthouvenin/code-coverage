# Parity with Codecov

`Compare-WithCodecov.ps1` compares a CodeCoverage instance with Codecov through both APIs (M4.06). It
replaces the SQL tool of the M1 pilot (M1.47): it reads no database, so it never reads across
organizations.

## What it compares

| Line | CodeCoverage | Codecov | Rule |
|---|---|---|---|
| Project and flag totals of a commit | `GET /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}` (`totals`, `flags`) | `GET /api/v2/{service}/{owner}/repos/{repo}/report/?sha={sha}` and `&flag={flag}` (`totals`) | coverage within `-Tolerance` points (0.01, bounds included): `ok`, else `gap`; the counters are reported `same` or `differ` |
| Patch totals of each file of a pull request | `GET /api/v1/repos/{forge}/{owner}/{repo}/pulls/{number}` (`files[].patch`) | `GET /api/v2/{service}/{owner}/repos/{repo}/compare/?pullid={number}` (`files[].totals.patch`) | identical hits, misses and partials: `ok`, else `gap`; when the two services compared different bases, the pull request is `base-differs` (`head-differs` for different heads) and its files are not judged; a file is matched on its head name, or its base name when the head lacks it |

Other statuses: `missing-codecov`, `missing-codecoverage`, `missing-both`. CodeCoverage does not serve
the patch lines (M3.10): a line-by-line gap found by hand is explained in
[docs/parity-log.md](../../docs/parity-log.md), with every other gap.

The shapes of the answers of Codecov come from its public documentation; they are checked at the first
real run.

## Running it

A dry run by default: it lists the calls it would make and reads nothing.

```bash
export COVERAGE_API_TOKEN=cvp_...   # a reader of the repository, in ONE organization
export CODECOV_API_TOKEN=...        # an API token of Codecov, not an upload token
pwsh tools/parity/Compare-WithCodecov.ps1 -ApiUrl https://coverage-api.example.com -Slug my-org/my-repo -Execute -OutputJson parity.json
```

- Without `-CommitsFile` (one full SHA per line), the 20 most recent commits CodeCoverage lists are
  compared (`-Count`); without `-Pulls 7,8`, its 20 most recent open pull requests.
- One organization per run: a `cvp_` token acts in the organization of its user. Run once per
  organization, with its own token.
- `-Anonymize` writes no repository name nor file path, for a report others can read.
- The script exits with 0 whatever it finds: a gap is a finding to explain, not a failure. An API
  that answers an error stops the run of that repository; with `-Anonymize`, the message names the
  side (codecov, codecoverage) and the status, never the address.

## Daily run

`.github/workflows/parity.yml` runs it every day (and on demand) for the repositories of
`PARITY_REPOSITORIES`, with the secrets `PARITY_COVERAGE_TOKEN` and `PARITY_CODECOV_TOKEN`; it is
skipped while they are not set. A failed repository is reported by its index only, the others still
run, the artifact is uploaded anyway, and the run fails at the end. Its artifact is anonymized: run it from a private copy of the
repository to keep full reports of private repositories. A scheduled workflow is disabled by GitHub
after 60 days without activity in the repository.

## Offline self-test

```bash
pwsh tools/parity/Test-CompareWithCodecov.ps1
```

It runs the comparison on the synthetic answers of `samples/` (see `samples/README.md`) and checks the
reference outputs of `samples/expected/`; the CI runs it on every pull request.
