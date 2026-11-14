# Frozen sample of the parity script

Synthetic, neutral answers (octo-org/app, made-up SHAs) of both APIs, read by
`Test-CompareWithCodecov.ps1` offline:

- `codecov/report-{sha}.json`, `report-{sha}.{flag}.json`, `compare-{number}.json`: shapes written from
  the documentation of the Codecov API v2; the compare answers follow its serializer
  (`api/shared/compare/serializers.py`: `base_commit`, `head_commit`, file `name` as `{base, head}`);
- `codecoverage/commit-{sha}.json`, `pull-{number}.json`: shapes of `CommitResponse` and
  `ComparisonResponse` of the Api;
- `expected/report.txt` and `expected/report.json`: the reference outputs.

The cases: a commit and its flag within the tolerance, a commit beyond it, a commit Codecov does not
have; a pull request with a file ok, a file with a different patch, a file only Codecov counts; a
pull request compared to different bases; a pull request whose head Codecov has not compared yet.
The file names of pull request 7 cover a rename (keyed on the head name), an added file and a file only
the base has (keyed on the base name).
