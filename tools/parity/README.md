# Parity with Codecov: totals of the M1 pilot

The exit criterion of M1 is « uploads from 3 pilot repositories: totals within ±0.01 % of Codecov on 20
commits ». `Compare-TotalsWithCodecov.ps1` measures it: for each commit, it compares the totals Codecov
holds with the rows of `CommitFlagTotals`, for the project and for each flag.

It is a tool of the pilot. A version that reads the API of CodeCoverage replaces it in M4.06.

## What it reads

| Side | Source | Option |
|---|---|---|
| Codecov | answers of its API v2, `GET /api/v2/{service}/{owner}/repos/{repo}/report/?sha={sha}` then `&flag={flag}` for each flag | `-CodecovToken` (or `CODECOV_API_TOKEN`), or `-CodecovSamplePath` for frozen answers |
| CodeCoverage | the rows of `CommitFlagTotals`, joined with their commit, repository and organization | `-Container` with `-Provider`, or `-TotalsPath` for the frozen output of the query |

Only the `totals` object of a Codecov answer is read: `lines`, `hits`, `misses`, `partials`, `coverage`.

## Running it

On the frozen sample, without network nor database:

```shell
pwsh tools/parity/Compare-TotalsWithCodecov.ps1 -Slug octo-org/app -CommitsFile tools/parity/samples/commits.txt `
  -CodecovSamplePath tools/parity/samples/codecov -TotalsPath tools/parity/samples/codecoverage/commit-flag-totals.json
```

On a pilot, against the database of the compose stack (`docker ps` gives the name of the container):

```shell
$env:CODECOV_API_TOKEN = '<API token of Codecov, not an upload token>'
pwsh tools/parity/Compare-TotalsWithCodecov.ps1 -Slug my-org/my-repo -CommitsFile pilot-commits.txt `
  -Provider PostgreSql -Container code-coverage-db-1 -OutputJson parity.json
```

`-Provider SqlServer` reads a SQL Server container the same way. The commits file holds one full SHA per
line; blank lines and lines starting with `#` are skipped.

The answers of Codecov can be frozen once and compared offline afterwards: save each answer as
`{sha}.json` (project) and `{sha}.{flag}.json` (flag) in a folder, and pass it as `-CodecovSamplePath`.

## The report

```text
Commit     Flag      Codecov  CodeCoverage Delta    Lines       Counts Carried Status
a1a1a1a1a1 (project) 85.71000 85.71429     +0.00429 14/14       same           ok
b2b2b2b2b2 front     75.00000 75.02000     +0.02000 10000/10000 differ         gap
```

- One line per commit and per flag; the project comes first. `Delta` is CodeCoverage minus Codecov, in
  percentage points.
- `Status` is `ok` within the tolerance (`-Tolerance`, 0.01 by default, bounds included), `gap` beyond it,
  `missing-codecov` or `missing-codecoverage` when one side has no totals for the line.
- `Counts` tells whether lines, hits, misses and partials are equal. They can differ with an equal
  percentage: it is shown, not counted as a gap, since the criterion is on the percentage.
- `Carried` marks a flag CodeCoverage carried forward from an ancestor.
- The gaps are listed after the table, then the count of commits within the tolerance and whether the
  criterion (`-RequiredCommits`, 20 by default) is met. A commit counts when its project and every one of
  its flags are `ok`.

The exit code is 0, unless `-FailOnGap` is given and a line is not `ok`. `-OutputJson` writes the lines
and the summary; `-PassThru` returns the lines as objects.

## Read-only access to the database

- The query is one `SELECT`, in `sql/commit-flag-totals.postgresql.sql` and
  `sql/commit-flag-totals.sqlserver.sql`. It takes no parameter and writes nothing.
- It runs inside the database container, with the client the image ships (`psql`, `sqlcmd`): nothing is
  installed on the host, and no password goes through the script.
- PostgreSQL: the session is opened with `default_transaction_read_only=on`, so a write would be refused
  by the server.
- SQL Server: the connection declares the `ReadOnly` application intent, which the server does not
  enforce on a single instance. Use a login that can only read (`-DatabaseUser`, member of
  `db_datareader`); the default `sa` of the compose overlay can write.
- The query reads every organization: it is an operator reading the database, outside the application
  and its organization filter. The script keeps the rows of `-Slug` and `-Forge` only.
- `Infrastructure.Tests/Parity/ParityQueryTests` runs both files against PostgreSQL and SQL Server.

To freeze the output of the query yourself:

```shell
docker exec -i -e PGOPTIONS="-c default_transaction_read_only=on" <db container> \
  psql -U cc -d codecoverage -At -q -f - < tools/parity/sql/commit-flag-totals.postgresql.sql > totals.json
```

## Differences with Codecov to keep in mind

- **Rounding.** Codecov answers a coverage rounded to 2 decimals; CodeCoverage stores 5. A difference
  under 0.005 point is this rounding, and is within the tolerance.
- **No coverable line.** Both sides give no coverage (`null`) for a report without line: the line is `ok`.
- **Carried forward flags.** Both products carry a flag forward from an ancestor commit; the ancestor
  they pick can differ when uploads are still being processed. The `Carried` column tells where to look.
- **Flag names.** A flag is compared by its exact name. A frozen answer is named after the flag, with
  every character other than a letter, a digit, `.`, `-` and `_` replaced by `_`.

## The frozen sample

`samples/` is **synthetic**: its Codecov answers follow the shape of the API v2 but were written for this
tool, not recorded from Codecov, and the real API is never called by the tests. Check the shape against
one real answer when the pilot starts.

| Commit | What it shows |
|---|---|
| `a1a1…` | project and two flags equal, up to the rounding of Codecov |
| `b2b2…` | a flag exactly at the tolerance (`ok`), another beyond it (`gap`) |
| `c3c3…` | the project beyond the tolerance, a flag carried forward and equal |
| `d4d4…` | unknown to Codecov |
| `e5e5…` | unknown to CodeCoverage, with a flag that only Codecov holds |
| `f6f6…` | no coverable line on either side |

`samples/codecoverage/commit-flag-totals.json` also holds rows of another organization, another forge,
another repository and a namespace: the report must leave them out.

`pwsh tools/parity/Test-CompareTotals.ps1` runs the script on the sample and checks it against
`samples/expected/report.txt` and `report.json`; `-Update` rewrites these references.
