# Changed lines of an Azure DevOps pull request (spike M7.01, Nov 2026)

Question (ADR-014, main risk of M7): how does CodeCoverage get the changed lines of an Azure DevOps
pull request, which patch coverage and `LineShifter` need, when the diff must come from the forge API
and never from the git history of the CI agent (AGENT.md, rule 2)?

> **Status: provisional.** No real organization has been measured yet. Everything below comes from
> the official documentation, or was reproduced offline with the tool of
> [tools/azure-devops-diff](../../tools/azure-devops-diff/README.md). The acceptance of M7.01 asks
> that the chosen path reproduce `git diff -U0` on at least nine pull requests out of ten. That
> number does not exist until the owner runs the tool (see [§8](#8-what-the-owner-runs)). The
> "Measured" columns stay empty until then.

Each claim is classified:

- **Proven (doc)**: written in the official reference (Microsoft Learn, REST API 7.1, or the
  documentation of the official client libraries);
- **Proven (local)**: reproduced offline by `Test-AzureDevOpsDiff.ps1` on this machine;
- **Probable**: inferred from the documentation or from how the service usually behaves, and still to
  be confirmed by the real run.

No claim about how the real service behaves is "proven" yet.

---

## 1. What CodeCoverage needs

- One entry per changed file: its path, its previous path (renames), its status and a unified patch.
  `UnifiedDiffParser` reads the patch and `DiffSet` holds it. `ForgeChangedFile.Patch` is `null` when
  the patch is unknown, and `Truncated` is set when the forge left files out.
- The **added lines** (numbered in the head) drive patch coverage. The **removed lines** (numbered in
  the base) drive `LineShifter`. Both must be those of `git diff`. They are compared here as two sets
  of line numbers per file.
- The base is the **merge base** of the target and the head (three-dot diff, as the GitHub pull
  request files API gives it). The head is the head of the pull request, never the synthetic merge
  commit.
- A file whose lines cannot be computed gives an **unknown** patch, never an empty one: an empty
  patch would show 100 % patch coverage.

## 2. The building blocks

| # | Claim | Class | Source |
|---|---|---|---|
| 1 | `GET pullRequests/{id}` gives `lastMergeSourceCommit` (the head) and `lastMergeTargetCommit`. | Proven (doc) | Pull Requests - Get |
| 2 | An iteration carries `sourceRefCommit`, `targetRefCommit` and `commonRefCommit`, "the first common Git commit of the source and target refs". | Proven (doc) | [Pull Request Iterations - Get][iter] |
| 3 | `iterations/{id}/changes` lists the changed files against the common commit by default (`$compareTo=0`), with `$top` (default 100, maximum 2000), `$skip`, and `nextSkip`/`nextTop`, which are 0 on the last page. | Proven (doc) | [Iteration Changes - Get][iterchanges] |
| 4 | `commonRefCommit` is frozen when the iteration is created. If the target moves later without a new push, it is older than the merge base of the current target and head. | Probable | (2), measured by `baseEqualsIterationCommonRef` |
| 5 | `diffs/commits` computes the merge base itself (`commonCommit`) and diffs from it when `diffCommonCommit=true`. | Proven (doc) | [Diffs - Get][diffs] |

The tool takes as base the `commonCommit` of `Diffs - Get` between `lastMergeTargetCommit` and the
head. It also records whether that base equals `git merge-base` and the iteration's
`commonRefCommit`.

## 3. The three paths

### 3.1 Path A: `Diffs - Get` with `diffCommonCommit=true`

`GET {org}/{project}/_apis/git/repositories/{repo}/diffs/commits?baseVersion={target}&baseVersionType=commit&targetVersion={head}&targetVersionType=commit&diffCommonCommit=true&$top=&$skip=&api-version=7.1`
(generally available).

| Topic | Claim | Class |
|---|---|---|
| Lines | `GitCommitDiffs` has `changes: GitChange[]`, `changeCounts`, `commonCommit`, `aheadCount`, `behindCount`, `allChangesIncluded`. `GitChange` has an `item` (`path`, `objectId`, `originalObjectId`, `gitObjectType`), `changeType` and `originalPath`. **No field carries lines or hunks.** | Proven (doc) |
| Consequence | Path A alone cannot give patch coverage. It gives the file list and the object ids that path C needs. | Proven (doc) |
| Binaries | No field marks a binary file. | Proven (doc) |
| Renames | `VersionControlChangeType` has `rename`, `sourceRename` and `targetRename`, and `GitChange` has `originalPath`. Whether Azure DevOps pairs renames the way `git diff -M` does (50 % similarity) is not documented. | Proven (doc) / Probable |
| Large files | No size per change, no truncation flag per file. | Proven (doc) |
| More than 100 files | `$top` defaults to 100 and `$skip` pages. Tree entries (folders) are listed with files, so they use up the pages. `allChangesIncluded` appears in the unpaged examples and is absent from the paged one. | Proven (doc, examples) |
| `allChangesIncluded = false` | Means the server cut the list. A hard cap per request is not documented. | Probable |
| Cost | `ceil(changes / $top)` requests. Their TSTU cost is not documented. | Proven (doc) / Probable |

### 3.2 Path B: `FileDiffs` (`lineDiffBlocks`)

`POST {org}/{project}/_apis/git/repositories/{repo}/FileDiffs?api-version=7.1-preview.1`, with
`{ baseVersionCommit, targetVersionCommit, fileDiffParams: [{ path, originalPath }] }`. It returns
`FileDiff[]`: `path`, `originalPath`, and
`lineDiffBlocks: [{ changeType, originalLineNumberStart, originalLinesCount, modifiedLineNumberStart, modifiedLinesCount }]`.
`changeType` is `None` (0), `Add` (1), `Delete` (2) or `Edit` (3).

| Topic | Claim | Class |
|---|---|---|
| Status | **Preview.** It is documented only by the client libraries: .NET `GetFileDiffsAsync` "[Preview API]", JavaScript `getFileDiffs`, and the Node client (location `c4c5a7e6-e9f3-4730-a92b-84baacff694b`, version `7.2-preview.1`). No REST reference page exists at the expected Learn address (404). A preview can change or disappear without notice. | Proven (doc) |
| Route | The route above comes from the client libraries. The tool reads the real one from the location service (`OPTIONS _apis/git`) and records its `releasedVersion`. | Probable |
| Lines | The blocks give the added and removed line ranges directly, so they are enough for patch coverage and `LineShifter`. | Proven (doc) |
| Patch text | The blocks carry no line text. A unified patch built from them alone has no hunk bodies, which `UnifiedDiffParser` requires. The bodies need the blobs anyway, or placeholder lines (a named difference). | Proven (doc + code) |
| Algorithm | The server's diff algorithm is not documented. Whether it equals git's (whitespace, end of lines, alignment of blank lines and braces) is unknown. | Probable: some differences |
| Binaries, large files | Not documented: no blocks, an error, or a cut answer. | Probable |
| Renames | `originalPath` is sent per file, so a rename is diffed against its old path. | Proven (doc) |
| More than 100 files | One request per batch. The batch limit is not documented; the tool sends 50 files per batch and records refusals. | Probable |
| Cost | `ceil(files / batch)` requests, TSTU unknown. | Probable |

### 3.3 Path C: two blobs per file, diffed locally

For each file of path A: `GET .../blobs/{originalObjectId}?$format=octetstream` and
`GET .../blobs/{objectId}?$format=octetstream` (generally available, `vso.code`). For a change without
an object id, `items?path=&versionDescriptor.version={commit}` is the fallback. The bytes are then
diffed in .NET.

| Topic | Claim | Class |
|---|---|---|
| API | `Blobs - Get` serves raw bytes (`$format=octetstream`) and the size (JSON). `resolveLfs` resolves LFS pointers on request. | Proven (doc) |
| Algorithm | `AzureDevOpsDiff.psm1` carries a port of git's xdiff with default options: trimmed ends, discarded lines without a match, the bidirectional Myers split with its cost heuristics, and group compaction with the indent heuristic. It reproduced `git diff --no-index -U0` on **300 out of 300** random edits of this repository's files, with and without the indent heuristic, and on the edge cases (missing final newline, CRLF to LF, duplicated braces, repeated blank lines). A plain forward Myers diff with slide-down matched only 53 of 60: reproducing git takes xdiff's own choices, not just a minimal diff. | Proven (local) |
| Same lines as git on Azure DevOps data | Follows from the line above, provided path A gives the same files, object ids and renames as git. | Probable |
| Binaries | git's rule: a NUL byte in the first 8000 bytes. Equals git without `.gitattributes`. A `binary` or `-diff` attribute is not read. | Proven (local) / named difference |
| Renames | The pairs come from path A. A rename that Azure DevOps reports as a delete plus an add gives a whole-file add, where git gives the edited lines only. That is a named difference unless CodeCoverage pairs files itself. | Probable |
| Large files | A cap per side (1 MiB in the tool) makes the patch unknown above it. | Design choice |
| LFS | A pointer is diffed as text, as git does without filters. | Probable |
| More than 100 files | Path A pages first, then 2 requests per modified file (1 for an added or deleted file). | Proven (doc) |
| Cost | The most requests and bytes of the three paths. Blobs are immutable and addressed by id, so they cache well across the commits of one pull request. | Proven (doc) / Probable |

## 4. Rate limits (TSTU)

- One TSTU is the average load of a typical user over five minutes. The limit is 200 TSTU per user (or
  per pipeline) in a sliding five-minute window. Beyond it, requests are delayed, then refused with
  HTTP 429 (`TF400733`). **Proven (doc)**, [rate limits][limits].
- `Retry-After`, `X-RateLimit-Resource`, `-Delay`, `-Limit`, `-Remaining` and `-Reset` are sent once a
  threshold applies, and `X-RateLimit-Cost` "if present". A run far from the limit may see none of
  them. **Proven (doc)**. The tool therefore records, per path, the requests, bytes and time as well as
  the sum of `X-RateLimit-Cost` and the number of responses that carried it.
- The "Basic + Test Plans" access level raises the limits of an integration identity. **Proven (doc)**.

## 5. Comparison

| | A: Diffs - Get | B: FileDiffs | C: blobs + xdiff |
|---|---|---|---|
| Changed lines | none | yes (ranges) | yes (ranges and text) |
| Same lines as `git diff -U0` | n/a | unknown (server algorithm) | proven locally; probable on Azure DevOps data |
| Unified patch with bodies | no | no (blocks only) | yes |
| Stability of the API | GA | preview | GA |
| Binaries | not flagged | undocumented | git's NUL rule, patch unknown |
| Renames | `rename` + `originalPath` | per file `originalPath` | pairs of path A |
| Large files | no size | undocumented | cap per side, patch unknown |
| More than 100 files | `$top`/`$skip`, `allChangesIncluded` | batches, limit unknown | follows A |
| Requests for N files | `ceil(N'/100)` (N' counts folders) | A + `ceil(N/batch)` | A + up to 2N |
| Measured: same lines as git (PRs out of 10) | — | — | — |
| Measured: TSTU per PR | — | — | — |

## 6. Side questions

### 6.1 Comment id: does the thread id suffice? (B9, M7.48)

- A thread (`GitPullRequestCommentThread.id`, int32) holds comments whose `Comment.id` is int16. The
  reference says comment ids "start at 1 and are unique to a pull request". Yet its own examples show
  two threads of one pull request (147 and 148) whose first comments both have the id 1. **Proven
  (doc)**: the reference contradicts itself.
- The comment is updated by `PATCH .../threads/{threadId}/comments/{commentId}`. So a thread that the
  application creates with a single comment is addressed by its thread id and the comment id 1.
  **Probable**.
- Evidence gathered: every existing thread of the measured pull requests, with its number of comments
  and its first comment id. A pull request with several threads all starting at 1 proves numbering per
  thread. `-ProbeThreadPull` creates two threads, updates comment 1 of each, then deletes them.
- If the run shows otherwise, M7.02 adds a micro-feature for a text comment id before M7.48
  (acceptance of M7.01).

### 6.2 Stable organization id in `_apis/connectionData` (M7.40)

- `connectionData` is the endpoint the client libraries use to find the services. It is **not** in the
  REST reference. It returns `instanceId` (the organization) and `authenticatedUser`. **Probable**.
- The documented id of an organization is `accountId` of [Accounts - List][accounts], which needs the
  member id of a person (`profiles/me`), so it is not available to a service principal. **Proven
  (doc)** for the shape, **probable** for the service principal.
- `instanceId` is a GUID that stays the same across renames of the organization. **Probable**. The tool
  records whether it is a GUID, whether it is stable across calls, and whether it equals `accountId`.
  It records booleans only, never the id. A GUID fits `ExternalAccountId` (40 characters, lower case).

### 6.3 Active pull request whose head is a commit (M7.47)

- `pullrequestquery` has two query types. `lastMergeCommit` searches "for pull requests that created
  the supplied merge commits". `commit` searches "for pull requests that merged the supplied commits".
  **Proven (doc)**. Neither one is "the active pull request whose head is this commit".
- `commit` may well find an active pull request whose head is the commit, but the wording ("merged")
  does not promise it. **Probable**, measured as `commitQueryFindsPull`.
- A documented alternative: `GET pullrequests?searchCriteria.status=active&searchCriteria.sourceRefName={ref}`,
  then a filter on `lastMergeSourceCommit.commitId`. **Proven (doc)** for the fields, measured as
  `listBySourceRefFindsPull`. The push event gives the ref. Without a ref, the filter runs over all
  active pull requests of the repository, one page at a time.

### 6.4 "Project Collection Administrators" membership with the application's token (M7.54)

- `HEAD https://vssps.dev.azure.com/{org}/_apis/graph/memberships/{subject}/{container}` answers 200 or
  404 (`vso.graph`, version `7.1-preview.1`). The group is found among `graph/groups`, and the
  subject's descriptor comes from `graph/descriptors/{storageKey}`. **Proven (doc)**.
- The check covers **direct** membership. A member through a nested group (an Entra group placed in
  the administrators group) needs `Memberships - List` with `direction=up`, followed level by level.
  **Probable**.
- Whether the application's identity (Basic access, no administration right) may read the graph is not
  documented. **Probable** yes for a member of the organization; the tool records each status code.
  The application of M7.12 does not exist yet, so the first run uses a person's PAT. That answers for a
  person's token, not the application's: this question stays probable until a second run with
  `-TokenKind Bearer`.

## 7. Provisional recommendation

1. **Path C**:
   - path A for the file list, the object ids and the base (`diffCommonCommit=true`, every page read);
   - `Blobs - Get` on both sides;
   - the patch computed in `Infrastructure/AzureDevOps` by a C# port of git's xdiff (the code
     validated here), producing a real unified patch that `UnifiedDiffParser` reads unchanged;
   - **no third-party package** (CONVENTIONS).
2. **Unknown patch, never empty**, for:
   - a binary file (NUL rule);
   - a side above the cap (**1 MiB**, an arbitrary starting value to confirm with the measured cost);
   - an unreadable blob;
   - a change without lines that Azure DevOps cannot pair.

   `Truncated` is set when `allChangesIncluded` is `false` or when the pull request has more than
   3,000 files (the GitHub limit, for the same behaviour).
3. **Path B** is not the source: it is a preview, it carries no line text, and its algorithm is not
   git's. It stays a cross-check during the spike.
4. **Base**: the `commonCommit` of `Diffs - Get` between `lastMergeTargetCommit` and the head, not the
   iteration's `commonRefCommit`, which can be older (2.4).
5. **Cost**: up to `2N + ceil(N'/100)` requests for N files, against one per 100 files on GitHub.
   Mitigations:
   - a cache by blob id (immutable);
   - added and deleted files cost one blob each.

   Confirm against the measured TSTU before M7.46.
6. **Side questions (provisional)**:
   - the thread id plus comment 1 for comments;
   - `instanceId` of `connectionData` for the organization;
   - the active list filtered on `lastMergeSourceCommit` for the head (`pullrequestquery` only if the
     run shows it finds active heads);
   - direct membership in the administrators group, plus nested groups through `direction=up`.

Named differences to carry into ADR-014 (M7.02) if the run confirms them:
- binary decided without `.gitattributes`;
- renames as Azure DevOps pairs them;
- patch unknown above 1 MiB per side.

## 8. What the owner runs

[tools/azure-devops-diff/README.md](../../tools/azure-devops-diff/README.md) gives the commands:

- a test organization with this public repository imported (its content can be published as fixtures);
- at least ten real pull requests made from consecutive commits of `develop`, plus one of more than 100
  files and one with a target that moved on and a binary file;
- a PAT set by the owner in `AZURE_DEVOPS_TOKEN`;
- `Measure-AzureDevOpsDiff.ps1 ... -Execute`.

The anonymized report fills in the "Measured" rows of §5 and settles each "probable" line above. The
fixtures of M7.46 are written by the same run: `tests/fixtures/azure-devops/diffs/{commits,blob-base,blob-head}.json`,
plus `filediffs.json`, `iteration-changes.json` and `patches/pr-<id>.diff`.

## References

- [Diffs - Get][diffs], [Blobs - Get Blob][blobs], [Pull Request Iterations - Get][iter],
  [Pull Request Iteration Changes - Get][iterchanges], [Pull Requests - Get Pull Requests][prs]
- FileDiffs: [.NET `GetFileDiffsAsync`][fdnet], [`FileDiffsCriteria`][fdcrit], [`LineDiffBlock`][ldb],
  [`LineDiffBlockChangeType`][ldbct], [Node client `GitApi.ts`][nodeapi]
- [Rate and usage limits][limits]
- [Pull Request Threads - Create][threads], [Pull Request Query - Get][prquery],
  [Memberships - Check Membership Existence][membership], [Accounts - List][accounts]
- git: xdiff (`xdiff/xprepare.c`, `xdiff/xdiffi.c`), the reference of the port

[diffs]: https://learn.microsoft.com/rest/api/azure/devops/git/diffs/get?view=azure-devops-rest-7.1
[blobs]: https://learn.microsoft.com/rest/api/azure/devops/git/blobs/get-blob?view=azure-devops-rest-7.1
[iter]: https://learn.microsoft.com/rest/api/azure/devops/git/pull-request-iterations/get?view=azure-devops-rest-7.1
[iterchanges]: https://learn.microsoft.com/rest/api/azure/devops/git/pull-request-iteration-changes/get?view=azure-devops-rest-7.1
[prs]: https://learn.microsoft.com/rest/api/azure/devops/git/pull-requests/get-pull-requests?view=azure-devops-rest-7.1
[fdnet]: https://learn.microsoft.com/dotnet/api/microsoft.teamfoundation.sourcecontrol.webapi.githttpclientbase.getfilediffsasync?view=azure-devops-dotnet-preview
[fdcrit]: https://learn.microsoft.com/javascript/api/azure-devops-extension-api/filediffscriteria
[ldb]: https://learn.microsoft.com/javascript/api/azure-devops-extension-api/linediffblock
[ldbct]: https://learn.microsoft.com/javascript/api/azure-devops-extension-api/linediffblockchangetype
[nodeapi]: https://github.com/microsoft/azure-devops-node-api/blob/master/api/GitApi.ts
[limits]: https://learn.microsoft.com/azure/devops/integrate/concepts/rate-limits?view=azure-devops
[threads]: https://learn.microsoft.com/rest/api/azure/devops/git/pull-request-threads/create?view=azure-devops-rest-7.1
[prquery]: https://learn.microsoft.com/rest/api/azure/devops/git/pull-request-query/get?view=azure-devops-rest-7.1
[membership]: https://learn.microsoft.com/rest/api/azure/devops/graph/memberships/check-membership-existence?view=azure-devops-rest-7.1
[accounts]: https://learn.microsoft.com/rest/api/azure/devops/account/accounts/list?view=azure-devops-rest-7.1
