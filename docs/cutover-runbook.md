# Cut-over from Codecov

The switch of one organization from Codecov to CodeCoverage (M4.08), then the cancellation of Codecov
once every organization has switched. Each organization switches at its own date: the prefix of its
statuses is one of its settings (ADR-013). Every step below is done by a person; the scripts of
`tools/rollout` only prepare or check, and are dry runs unless `-Execute`.

## 0. Before the cut-over (per organization)

- [ ] **Parity explained.** The daily parity of `tools/parity` shows no unexplained gap for the
      repositories of the organization; each gap is written in [parity-log.md](parity-log.md).
- [ ] **Dual upload everywhere.** Every repository uploads to both services (`tools/rollout/Add-CoverageUpload.ps1`).
- [ ] **Tokens imported.** The `CODECOV_TOKEN` values and the graphing tokens are imported
      (`tools/rollout/Import-CodecovTokens.ps1`, the badge tokens in the repository settings).
- [ ] **History imported.** `tools/rollout/Start-CodecovImport.ps1` ran for every repository and the
      history chart goes back before the migration. The import reads the API of Codecov: it must be
      done before the cancellation.
- [ ] **Checks permission accepted.** The installation of the organization accepted the Checks
      permission of the App (GitHub, settings of the installation).
- [ ] **Two-account checklist done.** [operations/github-verification-checklist.md](operations/github-verification-checklist.md)
      has no open line, and every correction it opened is in the plan.
- [ ] **Alerts wired.** The alerts of [operations.md](operations.md) reach someone.

## 1. Record the branch protections

```bash
gh auth login     # an account that can read the branch protections of the repositories
pwsh tools/rollout/Test-BranchProtection.ps1 -RepositoriesFile org-repos.txt -ExpectedAppId <id of the App of CodeCoverage> | tee protections-before.txt
```

The script is read-only. It lists every required check whose context starts with `codecov/`, in the
classic protection and in the rulesets, with the application it is pinned to:

- **any application**: the statuses of CodeCoverage under the `codecov` prefix will satisfy it;
- **pinned to another application** (Codecov's): the protection must be edited **before** the switch,
  to "any source" or to the App of CodeCoverage. This is the one case where the cut-over changes a
  protection; ADR-007 promised none, and it holds only for checks not pinned to Codecov.

Keep `protections-before.txt`.

## 2. Stop Codecov's notifications

Two writers on the same `codecov/*` names would overwrite each other. Before switching the prefix,
stop Codecov from publishing on the repositories of the organization, **on Codecov's side only**:

- **Remove the access of the Codecov App** to the repositories: in the settings of its installation on
  the GitHub account, deselect them ("Only select repositories"), or suspend the installation. Without
  write access, Codecov can no longer post a status, a check or a comment. This is the way to use.
- The organization YAML **in Codecov's own settings** only helps for the keys the repository
  `codecov.yml` does not set (the repository file wins over it): it is not enough when the repository
  file has `coverage.status` or `comment` sections.

**Never edit the repository `codecov.yml` to silence Codecov.** CodeCoverage reads the same file:
`coverage.status: off`, `comment: false` or `github_checks: false` there would silence CodeCoverage
too, and the required `codecov/*` checks would stay pending. The repository `codecov.yml` keeps its
`coverage.status`, `comment` and `github_checks` sections unchanged through the whole cut-over.

The upload to Codecov can stay until the cancellation; the history import reads the API of Codecov,
which the App access does not gate.

## 3. Switch

An administrator of the organization, in `/orgs/github/<org>/settings` (or
`PUT /api/v1/orgs/github/<org>/settings/{key}`):

1. `Notifications:StatusContextPrefix` = `codecov`;
2. `Notifications:ChecksEnabled` = `true` (the installation must have accepted the Checks permission;
   otherwise the statuses stay, logged, until it does).

The settings apply without restart. The next notification of each commit publishes `codecov/project`
and `codecov/patch` (as Check Runs with the Checks switch). For the open pull requests, re-notify their
heads from the pull request page (Re-notify) or with
`POST /api/v1/repos/github/<owner>/<repo>/commits/<sha>/notify`, so that their required checks turn
green without a new push.

## 4. Check the protections again

```bash
pwsh tools/rollout/Test-BranchProtection.ps1 -RepositoriesFile org-repos.txt -ExpectedAppId <id> | tee protections-after.txt
diff protections-before.txt protections-after.txt
```

The required checks must be unchanged (apart from the edits of step 1), and a test pull request must
show `codecov/project` and `codecov/patch` from CodeCoverage, satisfying the protection.

## 5. Remove the Codecov steps

```bash
pwsh tools/rollout/Remove-CodecovUpload.ps1 -RepositoriesFile org-repos.txt            # dry run: what would change
pwsh tools/rollout/Remove-CodecovUpload.ps1 -RepositoriesFile org-repos.txt -Execute   # one pull request per repository
```

The `CODECOV_TOKEN` secret stays: imported as an upload token, it may still authenticate the
CodeCoverage step.

## 6. Uninstall Codecov (per organization)

Once the pull requests of step 5 are merged: uninstall the Codecov GitHub App from the account, and
remove the organization from Codecov.

## 7. Cancel (once, after the last organization)

- [ ] Revoke the API token of Codecov used by the parity and the history import.
- [ ] Cancel the subscription of Codecov.
- [ ] Stop the scheduled parity workflow (`.github/workflows/parity.yml`): disable it, or unset its
      variables.

## Rollback (per organization)

Before step 6, the switch is reversible: set `Notifications:StatusContextPrefix` back to `coverage`,
`Notifications:ChecksEnabled` back to `false`, give the Codecov App its access to the repositories back
(step 2 reverted), and
re-notify the open pull requests. After the cancellation, there is no way back to Codecov.

## After the cut-over

Measure the volume of the pilot and adjust the retention settings (`Retention:RawDays`,
`Retention:BranchReportDays`, PLAN §11-8) and the backup size accordingly.
