# Onboarding an organization

How a GitHub account (an organization or a user) joins a CodeCoverage instance shared by several
isolated organizations (ADR-013). The instance administrator admits it; an organization administrator
then configures it. Nothing of one organization is visible from another.

## 1. Install the App

An owner of the GitHub account installs the GitHub App of the instance (`https://github.com/apps/<slug>`)
on all its repositories or on a selection. The App asks for:

| Permission | Why |
|---|---|
| Contents: read, Metadata: read | The diff, the trees, the `codecov.yml`, the annotated source. |
| Commit statuses: read & write | The statuses `coverage/*` (then `codecov/*`). |
| Pull requests: read & write | The pull request data and the comment. |
| Checks: read & write | The Check Runs, once the organization turns `Notifications:ChecksEnabled` on (M5.05). An App created before M5.05 gains it when the maintainers add it; each installation then accepts it from GitHub. |
| Organization members: read | The roles of the organization (admin, member) in the console. |

The installation creates a **pending** organization: it tracks nothing yet, and every upload is refused
(401 `Upload.TokenInvalid` with a token, `Repository.NotFound` with OIDC; the uploader still exits 0).

## 2. Admit it

The instance administrator admits the pending organization in `/admin/organizations`. Break-glass,
without the console (docs/operations.md, "The administration command line"):

```bash
dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
```

The account id is the numeric id of the GitHub account (`gh api users/my-org --jq .id`). The admission
starts the synchronization of the repositories of the installation.

## 3. Configure it

An administrator of the organization (an owner of the GitHub organization, or the user of a personal
account) opens `/orgs/github/<org>/settings`. The five settings of an organization:

| Setting | Default | When to change it |
|---|---|---|
| `Notifications:StatusContextPrefix` | `coverage` | `codecov` at the switch (docs/cutover-runbook.md). |
| `Notifications:StatusesEnabled` | `true` | Off to keep CodeCoverage silent on GitHub. |
| `Notifications:CommentsEnabled` | `true` | Off to keep the pull request comments of Codecov alone during the pilot. |
| `Notifications:ChecksEnabled` | `false` | On at the switch, once the installation accepted the Checks permission. |
| `Coverage:OrganizationYaml` | none | A YAML shared by every repository (under each repository's own `codecov.yml`). |

## 4. Tokens and badges

- **OIDC** needs nothing: a GitHub Actions job with `id-token: write` proves its repository.
- **Upload tokens:** import the `CODECOV_TOKEN` values (organization and repositories) in
  `/orgs/github/<org>/upload-tokens`, by the API, or with `tools/rollout/Import-CodecovTokens.ps1`.
  A value is unique within the organization only.
- **API tokens** (`cvp_…`, for the scripts) are created by each user in the console; they act with the
  rights of their user, in one organization.
- **Badge tokens** of private repositories: import the graphing token of Codecov in the settings of each
  repository, or rotate a new one.

## 5. What an organization cannot configure (ADR-013)

These follow from the isolation of the organizations and are the same for everyone:

- the App must be installed and the organization admitted before anything is accepted;
- no global upload token: a token works only in the organization that created or imported it;
- a transferred repository starts over in its new organization (empty history, no upload token, new
  badge token); its history stays with the old organization;
- after a rename of the GitHub account, the old login no longer resolves, and nothing redirects;
- the instance administrator sees metadata only (`/admin/operations`, `/admin/organizations`), never
  the coverage of an organization; the operators of the database, the backups and the monitoring see
  everything (docs/operations.md).

The reserved logins of the first design (a login equal to a route segment of the product) are gone:
the owner is no longer the first segment of a route (ADR-014).

## 6. Leaving

Uninstalling the App suspends the organization: its data is kept (no erasure yet), its uploads are
refused, and reinstalling it by the same account reattaches it.
