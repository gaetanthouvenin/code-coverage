# GitHub behaviours to verify on two real accounts (M4.02a)

ADR-013 rests on GitHub behaviours that could not be run from the repository ("Probable" in the ADR).
This list is executed by the owner on two real accounts before the cut-over (M4.08): account **A** (a
GitHub organization) and account **B** (a second organization, or a personal account where a line says
so), both with the App of a test instance installed. Each line is recorded as **confirmed**,
**corrected** (with the plan change it opens) or **not verifiable** (with its risk), and dated. A
confirmed line moves from "Probable" to "Proven" in ADR-013.

| # | Behaviour | Procedure | Expected (from the code) | Evidence to keep | Result | Date |
|---|---|---|---|---|---|---|
| 1 | SAML and `GET /user/installations` | On an organization with SAML single sign-on (GitHub Enterprise Cloud), sign in to the Web with a member who has **not** authorized SSO for the OAuth App, then with one who has. | Without SSO authorization, `user/installations` omits the organization: the user sees no repository of it (`GitHubUserGateway.ListInstallationsAsync`). With it, the organization is listed. | The two lists of `/` after sign-in; the API answer (`gh api user/installations` with the user token). | | |
| 2 | Outside collaborators in that list | Add an outside collaborator of one repository of A; sign in with that user. | `user/installations` lists A's installation with only the repositories the collaborator can read (`user/installations/{id}/repositories`); the organization role is none, the repository access read or write. | The list of repositories shown; the role resolved on `/orgs/github/A`. | | |
| 3 | Content of the `transferred` event | Transfer a repository from A to B. | A `repository` event `transferred` reaches the instance with the repository id unchanged and the new owner; `ReconcileRepositoryTransfer` closes the row in A, and `SyncInstallations` (transfer key) creates it in B (`HandleWebhookDeliveryCommand`). | The webhook delivery (Recent deliveries of the App) and the rows on `/admin/operations`. | | |
| 4 | `installation_repositories` removed, received by the origin | During the transfer of line 3. | A's installation receives `installation_repositories` with the repository in `repositories_removed` (when A had selected repositories). | The delivery. | | |
| 5 | `organization` renamed | Rename organization A. | An `organization` event `renamed` arrives; `RewriteRepositoryOwner` rewrites the owner of A's repositories; the old login no longer resolves. | The delivery; the new URL works, the old one answers not found. | | |
| 6 | `installation_target` renamed, on both account types | Rename A (organization) and B (personal account, if available). | An `installation_target` event `renamed` arrives for each; the organization row takes the new login (`GitHubInstallationTargetEvent`). | The deliveries; `/admin/organizations`. | | |
| 7 | Actions secrets after a transfer | Before line 3, add a repository secret `CODECOV_TOKEN` to the transferred repository. | Record whether GitHub keeps the secret after the transfer. Whatever it does, an imported repository token of A no longer works in B (refused), and the organization tokens of A are advised for rotation (`RotationAdvisedAt`). | The upload refusal in B; the banner in A. | | |
| 8 | A public repository of another account read by an installation token | With B's installation token, read a public repository of A that B has not installed on. | GitHub answers (public data); the instance never uses it, since every read goes through the installation of the repository's own organization. | The API answer. | | |
| 9 | The `/repositories/{id}` form on each endpoint | Run one notification of a pull request in A, then rename the repository and run another. | Every call below works by repository id before and after the rename (inventory below). | The calls in the logs (`github_requests_total` by endpoint) and the statuses after the rename. | | |

When line 1 cannot run (no GitHub Enterprise Cloud organization at hand), record it **not verifiable**
with its risk: a member without SSO authorization sees fewer repositories than they can read on GitHub;
nothing leaks.

## Inventory of the paths of the gateway

The calls of `GitHubAppGateway` (installation token unless said otherwise). A tracked repository is
always addressed by its id, so that a rename breaks nothing; only the lookup of a repository by name
uses owner and name.

| Call | Path |
|---|---|
| Manifest conversion (App JWT) | `POST app-manifests/{code}/conversions` |
| Installations of the App (App JWT) | `GET app/installations`, `GET app/installations/{id}`, `POST app/installations/{id}/access_tokens` (`GitHubAppTokenProvider`) |
| Repositories of an installation | `GET installation/repositories` |
| Lookup of a repository by name | `GET repos/{owner}/{name}/installation` (App JWT), then `GET repos/{owner}/{name}` |
| Membership of a user | `GET orgs/{org}/memberships/{user}` |
| Branch | `GET repositories/{id}/branches/{branch}` |
| Commits | `GET repositories/{id}/commits?sha=…`, `GET repositories/{id}/commits/{sha}` |
| Compare | `GET repositories/{id}/compare/{base}...{head}` |
| Pull request and its files | `GET repositories/{id}/pulls/{n}`, `GET repositories/{id}/pulls/{n}/files` |
| Trees and contents | `GET repositories/{id}/git/trees/{sha}`, `GET repositories/{id}/contents/{path}?ref=…` |
| Statuses | `POST repositories/{id}/statuses/{sha}` |
| Check runs (M5.05) | `POST repositories/{id}/check-runs`, `PATCH repositories/{id}/check-runs/{id}` |
| Comments | `GET\|POST repositories/{id}/issues/{n}/comments`, `PATCH\|DELETE repositories/{id}/issues/comments/{id}` |

`GitHubUserGateway` (user token): `GET user`, `GET user/installations`,
`GET user/installations/{id}/repositories`.
