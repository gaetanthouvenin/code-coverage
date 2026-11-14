# Parity log

Every gap the daily parity (`tools/parity/Compare-WithCodecov.ps1`, M4.06) reports between
CodeCoverage and Codecov is explained here, one by one, before the cut-over (M4.08). A gap is closed
when it is fixed in CodeCoverage, or explained as a named difference (docs/migration-from-codecov.md,
"Differences with Codecov").

No real run yet: the parity runs once the pilot uploads to both services (M4.07).

## How to record a gap

| Field | Content |
|---|---|
| Date | The day of the run. |
| Organization, repository | As in the full report (never in a public artifact for a private repository). |
| Line | Commit and flag, or pull request and file. |
| Status | `gap`, `base-differs`, `head-differs`, `missing-codecov`, `missing-codecoverage`. |
| Values | Both sides, as reported. |
| Cause | Proven, probable or unproven, with the evidence (a report, a log, a line of the source of Codecov). |
| Resolution | A fix (its issue or commit), a named difference, or "explained, nothing to do". |

## Known causes to check first

- **Different bases:** the two services compared the head to different commits (a base report missing
  on one side, a different fork point). The files are not judged; compare again once both have the
  same base.
- **Carryforward:** a flag carried forward on one side and uploaded on the other.
- **Patch lines:** CodeCoverage serves per-file patch totals, not the lines; a gap of a file is checked
  by hand against the annotated source of its pull request page.
- **Defaults profile:** a repository without `codecov.yml` gets the historical profile here (PLAN
  §11-7), not Codecov's defaults of 2024; statuses can differ, totals cannot.

## Gaps

| Date | Repository | Line | Status | Values (Codecov / CodeCoverage) | Cause | Resolution |
|---|---|---|---|---|---|---|
