# Self-hosted Codecov replacement on .NET 10: building blocks and GitHub integration (research, Nov 2026)

Scope: the concrete libraries, APIs and design choices for a single-container .NET 10 service with one database. It ingests Cobertura, LCOV, OpenCover, JaCoCo and Clover. It posts PR comments, commit statuses or checks and badges to GitHub, and receives uploads from both GitHub Actions and Azure Pipelines.

Versions were checked on nuget.org and docs.github.com / learn.microsoft.com in Nov 2026. Where a fact could not be confirmed, it is marked as unverified.

---

## 0. TL;DR recommendations

| Concern | Recommendation |
|---|---|
| GitHub identity | **One GitHub App** installed on the served GitHub organisation (`<owner>`, set in the web console; `my-org` in examples). It covers server-to-server work (comments, statuses/checks, compare, webhooks) and user login through the same app's "user access token" web flow. No PAT. No separate OAuth App. |
| GitHub client lib | **Octokit 14.0.0** (netstandard2.0, last released Jan 2025, still works) for REST calls. Sign the GitHub App JWT yourself (about 15 lines with `RSA.ImportFromPem` + `Microsoft.IdentityModel.JsonWebTokens`). Do not use the old `GitHubJwt` package. **Octokit.Webhooks.AspNetCore 4.2.3** (net8/net10, Oct 2026) for webhooks. Use raw `HttpClient` for anything Octokit lacks or models badly (for example the `compare` file list with pagination nuances). |
| Commit reporting | **Check Runs** (the Checks API needs a GitHub App) as the primary output, named `coverage/project` and `coverage/patch`, plus per-flag variants. Fall back to (or also emit) **commit statuses** with the same contexts, because branch protection works with both. Statuses are simpler and match Codecov's behaviour. A sensible MVP is statuses first, checks later. |
| Diff source | **Server-side, from the GitHub API** (PR files API, falling back to compare), so shallow clones (`fetchDepth: 1`) do not matter. Optionally the uploader also sends a locally computed unified diff when the GitHub API truncates. |
| Parsers | **Hand-written streaming `XmlReader` / line parsers** into one internal model. ReportGenerator.Core 5.5.x is Apache-2.0 and usable as a reference or test oracle, but it is a heavy, report-oriented dependency. |
| DB | **PostgreSQL 17/18 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.x** (10.0.1, Mar 2026) on EF Core 10. SQLite only as a dev/demo option. Store line coverage as **compressed binary blobs per (upload, file)**, with materialised **merged per-commit-per-flag totals** for history. |
| Web stack | **ASP.NET Core 10 minimal APIs + Blazor Web App in Static SSR mode, with selective Interactive Server islands** (charts, file tree). Single project, single container. |
| Background jobs | **DB-backed queue table + `BackgroundService`** using `FOR UPDATE SKIP LOCKED` (Postgres), with an in-process `Channel<T>` as a wake-up signal. No Hangfire needed. |
| Badges | Hand-rolled SVG (shields.io "flat" template) with an approximate width table. Cached, with `ETag`. |
| Upload auth | (1) **GitHub Actions OIDC** (no secret, verify against `https://token.actions.githubusercontent.com`). (2) **Per-repo upload token** (hashed in DB) for Azure Pipelines (secret variable) and as a universal fallback. (3) Optional later: an **org-level token** scoped to the GitHub org, validated by checking that the app is installed on the repo. |
| Packaging | One multi-stage Docker image (`mcr.microsoft.com/dotnet/aspnet:10.0`), a **`dotnet tool`** CLI (`System.CommandLine` 2.0.x stable), a **composite GitHub Action** that runs `dotnet tool install`, an Azure Pipelines template, plus a curl-only API for environments without .NET. |

---

## 1. GitHub integration

### 1.1 GitHub App vs OAuth App vs PAT

| Need | PAT (classic/fine-grained) | OAuth App | **GitHub App** |
|---|---|---|---|
| (a) Post/update PR comments | Yes, but posted as a human user; tied to a person leaving the company | Yes, as the user | **Yes, as `yourapp[bot]`**, through an installation token |
| (b) Commit statuses | Yes | Yes | **Yes** (`statuses: write`) |
| (b) Check Runs | **No** (Checks write is GitHub App only) | **No** | **Yes** (`checks: write`) |
| (c) PR metadata, files, compare on private repos | Yes | Yes | **Yes** (`pull_requests: read`, `contents: read`) |
| Webhooks (PR opened/synchronize, push, installation) | Repo/org hooks configured by hand | Same | **Built in, one URL for all installed repos** |
| (d) User login + "can this user see repo X?" | n/a | Yes (`repo` scope is very broad) | **Yes**: user access token + `GET /user/installations/{id}/repositories`, or `GET /repos/{o}/{r}` with the user token. Access is the intersection of the user's access and the app's installation. |
| Rate limits | 5,000/h per user | 5,000/h per user | Installation tokens scale (5,000/h minimum, more for large orgs/Enterprise Cloud: 15,000/h) |
| Secret rotation | Manual PAT expiry | Client secret | Private key (PEM) + client secret + webhook secret |

**Recommendation: a single GitHub App** (private to the org, "Only on this account"). It does all four jobs. Codecov itself works this way, through its "Codecov" GitHub App.

### 1.2 GitHub App configuration

**Repository permissions**
- `Contents: Read` for the compare API, file tree (`git/trees?recursive=1`, used for path normalisation), and reading `codecov.yml`/`coverage.yml` from the repo at a given sha.
- `Pull requests: Read & write`. Read is for PR metadata and files. Write is for PR comments, because PR comments are *issue* comments: `POST /repos/{o}/{r}/issues/{n}/comments` and `PATCH /repos/{o}/{r}/issues/comments/{id}`. They need `pull_requests: write` or `issues: write`. Pull requests write is enough for PR conversation comments.
- `Commit statuses: Read & write` for `POST /repos/{o}/{r}/statuses/{sha}`.
- `Checks: Read & write` for `POST /repos/{o}/{r}/check-runs` and `PATCH .../check-runs/{id}`.
- `Metadata: Read` (mandatory).
- Optional: `Actions: Read`. Only needed if you want to correlate with workflow runs; not required for OIDC.

**Organization permissions:** `Members: Read` (optional). Use it to show the org's repos to members or to gate admin features on team membership.

**Account permissions (for the user token):** usually none. Email is optional.

**Webhook events to subscribe to**
- `pull_request` (opened, synchronize, reopened, edited for base change, closed) gives PR head/base, triggers recomputation when the base moves, and stops commenting on closed PRs.
- `push` records branch heads, so you know the default-branch commit chain for "base" lookup and history even when no upload happened.
- `installation`, `installation_repositories` maintain the list of repos and installation IDs.
- `repository` (renamed, transferred, deleted, visibility change).
- `check_run` (rerequested) is optional. It lets users click "Re-run" on your check.

**User login (web UI):** use the same GitHub App's **Client ID / Client secret** with the web application flow (`https://github.com/login/oauth/authorize`). Use **AspNet.Security.OAuth.GitHub 10.0.0** (net10.0, Nov 2025) with `AddGitHub(o => { o.ClientId = ...; o.ClientSecret = ...; o.SaveTokens = true; })`. Scopes do not apply to GitHub Apps; permissions come from the app.
- With expiring tokens (the default for new apps), user access tokens **expire after 8 hours** and come with a **refresh token valid about 6 months**. Each refresh token works once.
- Repo-access check: `GET /user/installations/{installation_id}/repositories` (paginated) with the user token returns exactly the repos that both the user and the app can access. Cache it per user for about 5–15 minutes in `IMemoryCache` or a DB table, refreshed on login. Public repos can be shown without login if you want.
- Store an ASP.NET Core cookie session. Keep the GitHub user token server-side (encrypted with Data Protection; persist Data Protection keys to the DB with `PersistKeysToDbContext`, so restarts do not log everyone out).

### 1.3 Authentication mechanics (.NET)

1. **App JWT** (RS256, `iss` = App ID or Client ID, `iat` = now−60s, `exp` ≤ now+10min), signed with the PEM private key:
   ```csharp
   using var rsa = RSA.Create(); rsa.ImportFromPem(pem);
   var handler = new JsonWebTokenHandler();
   var jwt = handler.CreateToken(new SecurityTokenDescriptor {
       Issuer = appClientIdOrAppId,
       IssuedAt = DateTime.UtcNow.AddSeconds(-60),
       Expires  = DateTime.UtcNow.AddMinutes(9),
       SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
   });
   ```
   (Package `Microsoft.IdentityModel.JsonWebTokens` 8.x. The `GitHubJwt` NuGet package is unmaintained; skip it.)
2. **Installation token**: `POST /app/installations/{id}/access_tokens` returns a token valid **1 hour**. Cache it per installation and refresh at about 50 minutes. In Octokit: `new GitHubClient(...) { Credentials = new Credentials(jwt, AuthenticationType.Bearer) }`, then `client.GitHubApps.CreateInstallationToken(installationId)`, then a new client with `new Credentials(token.Token)`.
3. Wrap this in an `IGitHubClientFactory` that hands out a client per installation. Add Polly/`Microsoft.Extensions.Http.Resilience` handlers for 403/429 secondary-rate-limit back-off. Honour `Retry-After` and `x-ratelimit-reset`.

**Octokit status:** `Octokit` **14.0.0** (Jan 8, 2025) is the latest stable. It targets netstandard2.0 with no dependencies and works on net10.0. It covers Issues comments, Statuses, Checks (`client.Check.Run.Create`), PullRequest.Files, Repository.Commit.Compare, Git.Tree, and Repository.Content.
- Releases are infrequent. If you would rather not depend on it, raw `HttpClient` + `System.Text.Json` source generators for the ~10 endpoints you need is a small amount of code.
- A pragmatic option is Octokit for convenience, plus raw `HttpClient` for compare/files, where you want precise control of pagination and the `patch` field.
- `Octokit.Webhooks.AspNetCore` 4.2.3: subclass `WebhookEventProcessor`, override `ProcessPullRequestWebhookAsync` / `ProcessPushWebhookAsync`, then `app.MapGitHubWebhooks("/api/github/webhooks", secret)`. It validates `X-Hub-Signature-256`. **Do no work inline.** Enqueue to the job table and return 200 quickly, because GitHub expects a response within 10 s.

### 1.4 Statuses vs Check Runs

- **Commit status** (`POST /repos/{o}/{r}/statuses/{sha}`, body `{state: success|failure|pending|error, context: "coverage/patch", description: "85.20% of diff hit (target 80.00%)", target_url}`):
  - Simplest option. Works for any sha.
  - Description is limited to 140 characters.
  - At most 1000 statuses per sha+context.
  - Usable as a required check in branch protection/rulesets.
  - This is what Codecov does by default (`codecov/project`, `codecov/patch`).
- **Check Run** (`POST /repos/{o}/{r}/check-runs` with `name`, `head_sha`, `status`, `conclusion`, `output{title, summary (65,535 chars max), text, annotations}`):
  - Richer: markdown summary and **line annotations for uncovered changed lines**, at most 50 annotations per request; PATCH repeatedly to add more.
  - "Re-run" button support.
  - Checks created by an app appear in their own suite.
- **Recommendation:**
  - Implement statuses first. They are trivial and cover the threshold/gating requirement.
  - Add an optional Check Run per repo setting for annotations of uncovered patch lines, matching Codecov's "github_checks: annotations".
  - Use the same names, so branch protection rules can be migrated by renaming `codecov/patch` to `coverage/patch`. You could even keep the context names `codecov/project` and `codecov/patch`, so existing branch protection keeps working during migration.

### 1.5 PR comment

- Find-or-update: list issue comments (`GET /repos/{o}/{r}/issues/{n}/comments?per_page=100`). Look for a hidden marker `<!-- coverage-bot:comment -->` authored by your app bot, then PATCH it. Store the comment id in the DB to skip the search.
- Comment body limit: **65,536 characters**. Truncate the impacted-files table (top N by |delta| and patch misses) and link to the web UI.
- When to post: only when the head commit's expected uploads have arrived. Use a configurable `after_n_builds` (like Codecov's `comment.after_n_builds`) or a debounce of "no new upload for X seconds". Otherwise the comment flaps as each flag arrives.

---

## 2. Getting the diff for patch coverage without a clone

### 2.1 GitHub APIs (server side, preferred)

1. **PR files**: `GET /repos/{o}/{r}/pulls/{n}/files?per_page=100&page=N`
   - Returns at most **3000 files** in total (documented).
   - Each entry has `filename`, `status` (added/modified/removed/renamed/copied/changed/unchanged), `previous_filename`, `additions`, `deletions`, `changes`, and **`patch`** (unified-diff hunks).
   - The `patch` field **is not required**. It is omitted for binary files and for very large per-file diffs. GitHub does not document the size threshold; in practice it is around 1 MB / ~3000 lines of diff per file.
   - Community reports (2022) say `patch` disappeared for entries after the 70th when `per_page` > 70. Safer to use `per_page=30–50` and paginate.
   - The diff is relative to the **merge base** of base and head (three-dot), which is what you want for patch coverage.
2. **Compare**: `GET /repos/{o}/{r}/compare/{base}...{head}` (three-dot, merge-base semantics)
   - The file list appears **only on the first page** and is capped at **300 files** for the whole comparison.
   - The commit list is capped at 250 without pagination.
   - Use it for push builds on branches without a PR, and for "project delta" bookkeeping.
3. **Full diff media type**: `GET /repos/{o}/{r}/pulls/{n}` with `Accept: application/vnd.github.diff`. It returns **422 `too_large`** when the diff exceeds 300 files (or a size limit), so do not rely on it.

**Algorithm for patch coverage**
- Parse each file's `patch` hunks (`@@ -a,b +c,d @@`). Collect *added/modified* line numbers on the **head** side (lines starting with `+`).
- Patch coverage = among those lines, those that are *coverable* in the merged head report (have a hit count entry): `hits>0` / `coverable`. Partial branches count as partial, like Codecov.
- Lines not in the report (comments, blank lines) are ignored. Files absent from the report are ignored, or counted as missed if the config says so.

**Fallbacks when the API truncates** (more than 3000 files, or `patch` missing for a file)
- (a) Mark the file "diff unavailable" and exclude it, with a note in the comment.
- (b) **Accept a client-sent diff**: the uploader can send `git diff --unified=0 <merge-base>...HEAD`. That requires history, so it only works if the CI fetches enough depth (see below).
- (c) For a missing per-file `patch`, fetch both blobs (`GET /repos/{o}/{r}/contents/{path}?ref=` for base and head) and diff server-side with **DiffPlex** (NuGet, Apache-2.0). This is rarely needed.

### 2.2 Shallow clones (`fetchDepth: 1`)

- Server-side GitHub diffs **do not need any git history** on the agent. This is the main reason to compute diffs on the server, and Codecov does the same.
- The uploader only needs to send: `commit sha`, `branch`, `PR number` (if any), `build id/url`, `flag(s)`, and the reports.
  - GitHub Actions: `GITHUB_SHA`, `GITHUB_REF`, `GITHUB_HEAD_REF`, `GITHUB_BASE_REF`, PR number from `GITHUB_REF=refs/pull/123/merge`.
  - Azure Pipelines: `BUILD_SOURCEVERSION`, `BUILD_SOURCEBRANCH`, `SYSTEM_PULLREQUEST_PULLREQUESTNUMBER`, `SYSTEM_PULLREQUEST_TARGETBRANCH`, `SYSTEM_PULLREQUEST_SOURCECOMMITID`, `BUILD_REPOSITORY_PROVIDER=GitHub`, `BUILD_REPOSITORY_NAME=org/repo`.
- **Merge-commit pitfall:**
  - GitHub Actions `pull_request` events check out the synthetic **merge commit** (`refs/pull/N/merge`), so `GITHUB_SHA` is not the PR head sha. Uploads must be attributed to the **PR head sha** (`github.event.pull_request.head.sha`), which is where statuses must be posted for the PR UI. Codecov handles this the same way.
  - The uploader should detect `pull_request` events and read the head sha from `GITHUB_EVENT_PATH` JSON.
  - Azure Pipelines PR builds also build a merge commit (`BUILD_SOURCEVERSION` = merge). Use `SYSTEM_PULLREQUEST_SOURCECOMMITID` for the head sha.
  - Line numbers in a merge-commit coverage report match the head file when the base did not touch the same files, which is the usual case. Codecov accepts this approximation too.
- **Base commit for the project delta:**
  - Use the PR's `base.sha` from the webhook/API (or merge-base from compare: `merge_base_commit.sha`).
  - Then find the **nearest ancestor of base that has a complete report**. Walk the parent chain with stored `push` data or `GET /repos/{o}/{r}/commits?sha=base&per_page=20`.
  - If none is found within N commits, report "base report missing".

---

## 3. Parsing the five formats

### 3.1 Library vs hand-written

- **ReportGenerator.Core** (NuGet `ReportGenerator.Core` **5.5.x**; the CLI is at 5.5.4)
  - **License:** **Apache-2.0** at project level. Confirm the `license` field in the exact `.nupkg`.
  - **Target framework:** netstandard2.0, depending on Microsoft.Extensions 10.0.0 packages.
  - **Formats:** it parses all five plus many more (dotCover, Visual Studio coverage XML, gcov, etc.).
  - **Drawbacks:**
    - The API is designed to *generate reports*: `Generator().GenerateReport(ReportConfiguration)`, with parser classes such as `CoberturaParser`, `OpenCoverParser`, `LCovParser`, `JaCoCoParser`, `CloverParser` under `Palmmedia.ReportGenerator.Core.Parser`.
    - The internal model (`Assembly` → `Class` → `CodeFile` → line arrays) is usable but not a stable public contract.
    - It loads documents with `XDocument` (whole DOM). That is fine for typical sizes, but large monorepo reports (hundreds of MB) use a lot of memory.
    - Some ReportGenerator *output* features are under a PRO license. Not relevant to parsing.
  - **Use it as:** a reference implementation and a **test oracle** in unit tests (compare totals).
- **Recommendation: hand-written parsers** (one class per format, about 150–300 lines each):
  - `XmlReader` with `DtdProcessing = Ignore`/`Prohibit` and `XmlResolver = null`. Cobertura and Clover files sometimes carry a DOCTYPE; never resolve DTDs (XXE).
  - LCOV via a `StreamReader` line loop.
  - Output an internal model: `Dictionary<string path, FileCoverage>`, where `FileCoverage` = sorted `line → (hits, branchesCovered, branchesTotal)` plus optional method entries.
  - **Merge rule within a single report:** max of hits? No. Codecov uses "sum hits, union branches". Simplest correct approach: hits = `max` when the same line appears multiple times in *one* report (because of duplication; see Cobertura below), `sum` or `max` across *uploads* (either works for covered/not-covered; Codecov effectively treats a line as hit if any session hits it). Branches: covered = max(coveredA, coveredB) when totals are equal.
- Autodetect the format by sniffing: root element `coverage` with `line-rate` → Cobertura; `coverage` with `clover` attribute or `<project>` child → Clover; `CoverageSession` → OpenCover; `report` (JaCoCo DOCTYPE) → JaCoCo; text starting with `TN:` or `SF:` → LCOV. Also accept gzip input.

### 3.2 Per-format pitfalls

**Cobertura** (coverlet, Jest/istanbul `cobertura` reporter, ReportGenerator `Cobertura` output, Python coverage)
- Structure: `coverage > sources/source* > packages > package > classes > class[@filename] > (methods > method > lines > line) + lines > line[@number @hits @branch @condition-coverage="50% (1/2)"]`.
- **Duplication:**
  - Lines appear both under `method/lines` and under `class/lines`. Read only the class-level `<lines>`, or dedupe.
  - The same file appears in **multiple `<class>` elements**: partial classes, nested classes, compiler-generated async state machines and lambdas (`Foo/<Bar>d__3`), and the same file in multiple packages (multi-targeted assemblies).
  - Aggregate per `filename` and per line: hits = **max**, branches = max covered over the same total.
  - Never add class-level totals.
- `filename` is relative to one of the `<source>` entries. Coverlet emits `<source>` as absolute roots like `/home/runner/work/repo/repo/src/`. With `DeterministicSourcePaths`/`ContinuousIntegrationBuild=true` it emits `/_/src/...` style paths.
- `condition-coverage` must be parsed with a regex `\((\d+)/(\d+)\)`.
- Some generators emit `branch="true"` without `condition-coverage`; treat that as 0/0.
- Ignore the file-level `line-rate`/`branch-rate`; recompute everything.

**OpenCover** (coverlet `opencover`, OpenCover itself, AltCover)
- `CoverageSession > Modules > Module > Files > File[@uid @fullPath]` plus `Classes > Class > Methods > Method > SequencePoints > SequencePoint[@vc @sl @el @fileid] + BranchPoints > BranchPoint[@vc @sl @path @offset @fileid]`.
- Map `fileid` to the module-level `File` uid. Uids are **per module**, so keep a map per module.
- A sequence point spans `sl..el` lines. Codecov/ReportGenerator attribute it to lines sl..el, or only sl. Choose **sl..el, hits = max(vc)**.
- Branch points: group by `sl`. Total = count of points, covered = count with `vc>0`.
- Skip `Module[@skippedDueTo]` and `Method[@skippedDueTo]`.
- Coverlet emits **`<Method visited="false">` with no `fileid`** for some generated code; skip anything without a fileid.
- Methods marked `isGetter/isSetter` are fine.
- Watch for `Module` duplicates when tests from several projects are merged (same file seen from different modules); merge by max.

**LCOV** (Jest, Vitest `lcov`, c8/nyc, coverlet `lcov`, genhtml)
- Records:
  - `TN:`, `SF:<path>`
  - `FN:<line>,<name>` (newer format `FN:<start>,<end>,<name>`)
  - `FNDA:<hits>,<name>`, `FNF/FNH`
  - `DA:<line>,<hits>[,<checksum>]`, `LF/LH`
  - `BRDA:<line>,<block>,<branch>,<taken|->`, where `-` means "never evaluated" and counts as 0. `BRF/BRH`.
  - `end_of_record`.
- Multiple `SF` blocks for the same file (multiple test runs concatenated): merge by summing or maxing DA.
- `DA` hits can exceed int32; use `long`.
- Paths:
  - Jest emits **absolute paths** (`/home/runner/work/x/x/frontend/src/a.ts`).
  - Vitest is usually relative to the Vitest root (for example `src/a.ts`, relative to `frontend/`, **not to the repo root**). The path fixer must try suffix matching.
- Windows agents produce `SF:D:\a\1\s\src\a.ts` with backslashes.
- Ignore `LF/LH/BRF/BRH` and recompute.

**JaCoCo** (for completeness, plus any Kotlin/Java)
- `report > group* > package[@name="com/foo"] > sourcefile[@name="Bar.java"] > line[@nr @mi @ci @mb @cb]`. The `class/method/counter` elements are *aggregates*; ignore them.
- Line covered = `ci>0`; coverable = `mi+ci>0`. Branches total = `mb+cb`, covered = `cb`.
- **There is no source root, only `package/name + sourcefile/name`**, so path resolution **must** suffix-match against the repo file list (`src/main/java/com/foo/Bar.java`, or `module-x/src/main/kotlin/...`).
- The DOCTYPE references `report.dtd`; must ignore it without fetching.

**Clover** (istanbul `clover` reporter, PHPUnit, OpenClover)
- `coverage > project > (package >)? file[@name @path?] > line[@num @type="stmt|cond|method" @count | @truecount @falsecount]`.
- `stmt`: coverable, hits = count.
- `cond`: branches. 2 branches, covered = (truecount>0) + (falsecount>0). Some generators also give `count`.
- `method`: treat as a function entry, **not as a coverable line**. Codecov ignores method lines for line coverage, otherwise a method signature counts as covered.
- The `file@name` in istanbul output is often the base name with `@path` absolute. PHPUnit puts the absolute path in `name`.
- `metrics` elements are aggregates; ignore them.

### 3.3 Path normalisation (absolute CI paths → repo-relative)

Codecov's approach ("path fixing") is to match report paths against the repo's file list ("network"). Implement:
1. **Get the repo file list at the head sha**: `GET /repos/{o}/{r}/git/trees/{sha}?recursive=1`. It is truncated at about 100k entries / 7 MB, and the response has `truncated: true` when that happens. Cache per tree sha. Alternatively the uploader sends `git ls-files`, which works even with depth 1, and is free and authoritative. **Do both:** the uploader sends `git ls-files` when it can; the server falls back to the tree API.
2. Normalise the separator `\` → `/`, collapse `./` and `../`, and strip the drive letter.
3. Strip known CI prefixes:
   - `/home/runner/work/{repo}/{repo}/` (GH-hosted Linux)
   - `D:\a\{repo}\{repo}\` (GH-hosted Windows)
   - `/__w/{repo}/{repo}/` (container jobs)
   - Azure `$(Build.SourcesDirectory)` → `/home/vsts/work/1/s/`, `D:\a\1\s\`, `C:\agent\_work\N\s\` (self-hosted)
   - Deterministic `/_/`
   - The Cobertura `<source>` values

   Better: the uploader sends its **working directory / repo root** (`git rev-parse --show-toplevel`, `GITHUB_WORKSPACE`, `BUILD_SOURCESDIRECTORY`), so the server strips exactly that.
4. If the result is not in the file list: **longest-suffix match** of path segments against the file list (index files by reversed segments in a trie or dictionary keyed by file name). Accept a unique best match; on a tie, prefer the shortest repo path, or the one under the flag's `paths` config.
5. Apply user `fixes:` rules from the YAML config (Codecov syntax: `"before/::after/"`) and `ignore:` globs (use `Microsoft.Extensions.FileSystemGlobbing`, or `DotNet.Glob`).
6. Drop files that cannot be resolved, report them in the upload's processing log, and show that log in the UI.

Path matching should be **case-sensitive** after normalisation (Linux repos), with a case-insensitive second pass for Windows-generated reports.

---

## 4. Storage

### 4.1 PostgreSQL vs SQLite

- **PostgreSQL (recommended):**
  - Concurrent writers: parallel uploads from matrix jobs and webhooks at once.
  - `SKIP LOCKED` job queue.
  - `bytea` for blobs (TOAST handles compression and out-of-line storage, but compress yourself anyway for predictability).
  - Good window functions for history queries. Easy managed hosting: Azure Database for PostgreSQL Flexible Server, or a `postgres:17`/`postgres:18` container.
  - Provider: **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1** (10.0.0 Nov 2025, 10.0.1 Mar 2026) on **EF Core 10**.
- **SQLite:**
  - Tempting for "one container, zero DB". Fine for a demo.
  - But there is a single writer (WAL mode helps reads only), no `SKIP LOCKED`, and backup/restore on a VM volume is your problem.
  - Option: keep the code provider-agnostic, with no Postgres-specific EF features except the queue query, which can be abstracted. Ship `docker-compose.yml` with Postgres as the default.

### 4.2 Data model (sketch)

```
Repository(Id, GitHubId, Owner, Name, DefaultBranch, InstallationId, UploadTokenHash, ConfigYamlCache, Private, Active)
Commit(Id, RepoId, Sha, ParentShas[], Branch, PullNumber?, Author, Message, Timestamp, State[pending|processed|error])
Upload(Id, CommitId, Flags[], Name, CiProvider, BuildUrl, JobCode, Format, RawBlobRef, Status, ReceivedAt, ProcessedAt, Error)
UploadFile(UploadId, Path, Lines int, Hits int, Partials int, BranchesTotal, BranchesCovered, Coverage blob)   -- per file per upload
CommitReport(CommitId, Flag (''=all), Totals jsonb/columns, FileTotals blob, CarriedForwardFrom?)  -- merged, materialised
PullRequest(Id, RepoId, Number, HeadSha, BaseSha, State, CommentId?, CheckRunIds...)
PullComparison(PullId, HeadCommitId, BaseCommitId, ProjectDelta, PatchHit, PatchTotal, ImpactedFiles blob)
Job(Id, Type, PayloadJson, RunAfter, Attempts, LockedUntil, LockedBy, Status)
```

### 4.3 Compact line coverage

- **Per (upload, file) blob.** Encode a sorted list of coverable lines: delta-encoded line numbers (varint), hits (varint, or capped/clamped), and an optional branch pair (cov/total, varint). Compress with **Brotli** (`System.IO.Compression.BrotliEncoder`, quality 4–5) or **ZLib/GZip**.
  - Typical result: a few hundred bytes per file.
  - Everything is built in; no NuGet needed.
  - Alternative: `int[]` bitsets (covered/coverable/partial) per file, simpler but loses hit counts. Codecov only displays hit/miss/partial in practice.
- **Merged per commit per flag:** compute on processing and store a `CommitReport` per flag plus an "all flags" merged one. Contents: totals columns (lines, hits, misses, partials, coverage %) and a single compressed blob holding all files' line arrays, or a `CommitFile` table with per-file blobs (better for viewing a single file).
  - Recompute only when a new upload arrives for that commit.
- **Keep raw reports** (gzipped) in the DB or on disk/Azure Blob for N days, for reprocessing after parser fixes. Use a `RawStore` abstraction: filesystem volume by default, Azure Blob optional.
- **Carryforward:**
  - When commit C has no upload for flag F (and F has `carryforward: true`), the merged report for C copies F's `CommitReport` from the nearest ancestor that has one: parent chain, using the `parents` array from `push` webhook payloads or the GitHub commits API.
  - Mark it `CarriedForwardFrom`.
  - Codecov behaves exactly this way. It matters for monorepos where only the frontend pipeline runs.
- **History/charts:**
  - Query `CommitReport` totals for the default branch ordered by commit timestamp. It is already tiny: one row per commit per flag.
  - For long ranges, down-sample in SQL: `date_trunc('day', ts)` + last value per day via `DISTINCT ON`.
  - No separate time-series store is needed.
- **Retention:** prune `UploadFile` blobs for non-default-branch commits older than 90 days. Keep `CommitReport` totals forever.

EF Core 10 features worth using:
- `ExecuteUpdate`/`ExecuteDelete` for pruning and job state.
- Complex types / JSON columns (`ComplexProperty(...).ToJson()`) for config and totals.
- Compiled models only if startup matters.
- Primitive collections (`string[] Flags` → Postgres `text[]`).
- Run migrations at startup behind a config flag, or with an `efbundle` in the image.

---

## 5. Web stack (.NET 10)

### 5.1 UI options

| Option | Fit |
|---|---|
| **Blazor Web App, Static SSR + Interactive Server islands** (recommended) | One project; C# end to end; server-rendered pages for repo list, commit view, file source view with coverage gutters, and PR comparison. Interactivity only where it helps: chart range picker, file tree filter. With few users, the Server circuit cost is irrelevant. Works with cookie auth out of the box. |
| Razor Pages | Also fine and very simple, but you would hand-write JS for interactivity. |
| Blazor WASM | Larger download, needs an API layer. No benefit for an internal tool. |
| Separate SPA (React/Vite) | Second toolchain and build. Avoid, given the small team. |

- Charts: a JS lib through a tiny interop (Chart.js 4 from the cdnjs/jsdelivr bundle, served locally from `wwwroot`), or render **server-side SVG** sparklines/line charts yourself, which fits Static SSR.
- File view: fetch file content from GitHub (`GET /repos/{o}/{r}/contents/{path}?ref={sha}`, raw media type `application/vnd.github.raw+json`) **with the installation token**, after checking the user's access. Render lines with hit/miss/partial CSS classes. Syntax highlighting is optional (highlight.js client-side).

### 5.2 API surface (minimal APIs)

- `POST /api/v1/upload`: multipart or raw gzip body. Query/headers carry `sha`, `branch`, `pr`, `flags`, `name`, `build_url`, `service`, `slug` (owner/repo), `root` (working dir), `format` (optional). Returns 202 + upload id. Add `RequestSizeLimit` (for example 200 MB) and accept `Content-Encoding: gzip`.
- `GET /api/v1/repos/{owner}/{repo}/badge.svg?branch=&flag=`
- `GET /api/v1/repos/{owner}/{repo}/commits/{sha}/totals` (JSON, for scripts)
- `POST /api/github/webhooks` (Octokit.Webhooks)
- `GET /healthz` (`AddHealthChecks().AddDbContextCheck<>()`)

### 5.3 Background processing

- `Job` table + one `BackgroundService` per worker type (or one generic worker with N concurrent loops).
  - Dequeue: `UPDATE job SET locked_by=@me, locked_until=now()+'5 min' WHERE id = (SELECT id FROM job WHERE status='queued' AND run_after<=now() ORDER BY id FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *`. Run it as raw SQL through EF's `FromSql` / `Database.SqlQuery`.
  - A `Channel<long>` signals the worker immediately after enqueue (no polling latency). Poll every few seconds as a fallback; this also survives restarts.
  - Retries: exponential back-off via `run_after`, plus a max attempt count.
- Pipeline of jobs:
  1. `ProcessUpload` (parse, normalise, store `UploadFile`)
  2. `MergeCommit` (debounced per commit: coalesce with `ON CONFLICT DO NOTHING` on a unique `(type, key)` for pending jobs)
  3. `NotifyCommit` (statuses/checks)
  4. `NotifyPull` (comparison + comment)
- Serialise per repo+commit to avoid races. A Postgres advisory lock (`pg_advisory_xact_lock(hash(commit_id))`) is the simplest option.
- **Hangfire** (1.8.x, Postgres storage via `Hangfire.PostgreSql`): has a nice dashboard, but adds two dependencies and its own schema. Not needed at this scale. Choose it only if you want the dashboard.

### 5.4 SVG badges

- Write it by hand. Use the shields.io "flat" layout: two `<rect>` (label grey `#555`, value colour), a `linearGradient` overlay, `<text>` in Verdana/DejaVu Sans 11px with a shadow copy.
- Width: estimate with a per-character width table for Verdana 11px (shields publishes the widths; roughly 6–7 px average), or measure with **SkiaSharp**. That is overkill; avoid the native dependency.
- Colour scale like Codecov's `coverage.range: 60..90` (red → yellow → green, interpolated).
- Headers: `Content-Type: image/svg+xml`, `Cache-Control: no-cache, max-age=300` (GitHub's camo proxy caches aggressively; `no-cache` + `ETag` makes it revalidate), and an `ETag` from the coverage value.
- **Private repos:** badges require a token. Use `badge.svg?token=<per-repo random badge token>`, different from the upload token, as Codecov does with its "graph token".
- Optionally also serve a **sunburst/tree SVG** later.

---

## 6. Upload authentication

1. **GitHub Actions OIDC (no secret)**
   - The workflow needs `permissions: id-token: write` (plus `contents: read`).
   - The uploader requests a token from `$ACTIONS_ID_TOKEN_REQUEST_URL&audience=<your-service-url>` with `Authorization: bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN`, then sends it as `Authorization: Bearer <jwt>`. In a composite action you can also call `core.getIDToken` via `actions/github-script`; the plain curl to the env vars is simpler.
   - Server: `AddAuthentication().AddJwtBearer("github-oidc", o => { o.Authority = "https://token.actions.githubusercontent.com"; o.TokenValidationParameters = new() { ValidAudience = "<your-service-url>", ValidIssuer = "https://token.actions.githubusercontent.com" }; })`. Discovery is at `/.well-known/openid-configuration`, which yields the JWKS.
   - Authorise: claim `repository` (`my-org/foo`) must equal the upload slug. Also check `repository_owner` = the served organisation (`my-org`), and match `repository_id` against the stored GitHub repo id (rename-safe).
   - Optional checks: `event_name`, `ref`.
   - Note there is **no plain `sha` claim** (there is `workflow_sha`, `job_workflow_sha`); trust the sha from the request body, since the repo identity is what OIDC proves.
   - Default `aud` is the repo-owner URL if not customised; always request a custom audience.
   - **Fork PRs:** the `pull_request` event from forks gets no `id-token: write`, so no OIDC. Forks are probably irrelevant for private org repos; otherwise fall back to tokenless-for-forks (Codecov accepts it for public repos) or skip.
2. **Per-repo upload token** (works everywhere: Azure Pipelines, local, other CI)
   - Random 32 bytes, base64url, prefixed for secret scanning (`cvg_...`). Store a **SHA-256 hash** only. Show it once in the repo settings UI; it can be rotated.
   - Azure Pipelines: store it as a **secret pipeline variable** or in a **variable group** (optionally linked to Azure Key Vault), and pass it as an env var. Secret variables are not auto-exported to scripts, so map them explicitly: `env: { COVERAGE_TOKEN: $(COVERAGE_TOKEN) }`.
   - Consider also an **org-level upload token** (one secret in a shared variable group for all repos). The server then accepts any slug whose repo belongs to an org where the app is installed. Codecov added the same "global upload token" for simplicity. Trade-off: one leaked token can poison any repo's coverage. That is acceptable for an internal tool and greatly simplifies onboarding many repos.
3. **Azure Pipelines OIDC** (possible, but not worth it for v1)
   - Azure DevOps can mint OIDC tokens only in the context of a service connection: the `Oidctoken` REST API (`distributedtask/oidctoken`, api-version 7.2), with `System.OidcRequestUri` and `System.AccessToken`.
   - Issuer: `https://vstoken.dev.azure.com/{orgId}`. Audience: fixed `api://AzureADTokenExchange`. Subject: `sc://{org}/{project}/{serviceConnection}` (or `p://org/project/pipeline` in newer variants).
   - It proves the *Azure DevOps project/service connection*, not the GitHub repo, so you would still map it to repos.
   - **Recommendation:** use a secret token for Azure Pipelines.

Abuse protection: rate-limit uploads per token (`Microsoft.AspNetCore.RateLimiting`), cap the body size, and reject unknown repos.

---

## 7. Packaging

### 7.1 Docker image (single container)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Coverage.Server -c Release -o /app /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0   # or :10.0-noble-chiseled for minimal image
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Coverage.Server.dll"]
```

- Config through env vars: `ConnectionStrings__Db`, `GitHub__AppId`, `GitHub__ClientId`, `GitHub__ClientSecret`, `GitHub__PrivateKey` (PEM, or a file path mounted as a secret), `GitHub__WebhookSecret`, `PublicUrl`.
- `docker-compose.yml` with `postgres:17` (or 18) + the app + a volume.
- Hosting options:
  - Azure Container Apps (scale-to-1, managed certs) + Azure Database for PostgreSQL Flexible Server (Burstable B1ms is enough).
  - Or a VM with Caddy for TLS.
- The webhook endpoint must be reachable from GitHub (public HTTPS). If the service must be private, restrict by GitHub's hook IP ranges (`GET /meta` → `hooks`) and keep the UI behind SSO/VPN.
- Dev: use smee.io or `gh webhook forward` for webhooks to localhost.
- Alternatively, `dotnet publish /t:PublishContainer` (SDK container support) builds the image without a Dockerfile.

### 7.2 CLI uploader as a dotnet tool

- Project: `<PackAsTool>true</PackAsTool><ToolCommandName>coverage-upload</ToolCommandName>`.
  - Target `net8.0;net10.0`, so it runs on agents with either runtime. Or use `<RollForward>Major</RollForward>`.
  - Published to a feed: GitHub Packages NuGet or Azure Artifacts, or nuget.org if public. GitHub-hosted and Microsoft-hosted agents have the .NET SDK preinstalled.
  - Also publish **self-contained single-file executables** (`-r linux-x64`, `win-x64`, `osx-arm64`, `PublishSingleFile`, optionally NativeAOT) as GitHub release assets, for agents without the .NET SDK (JS-only pipelines).
- CLI parsing: **System.CommandLine 2.0.x** (stable; 2.0.10 on nuget.org, GA around Nov 2025, with the new `RootCommand`/`Option<T>`/`SetAction` API). Or keep it dependency-free.
- Behaviour:
  1. Find reports by glob (`**/coverage.cobertura.xml`, `**/lcov.info`, `**/coverage.opencover.xml`, `**/jacoco*.xml`, `**/clover.xml`), or take `-f` paths.
  2. Detect the CI provider and read env:
     - GitHub: `GITHUB_*`, `GITHUB_EVENT_PATH` for the PR head sha.
     - Azure: `BUILD_*`, `SYSTEM_PULLREQUEST_*`; `BUILD_REPOSITORY_NAME` gives `org/repo` when the provider is GitHub.
  3. Gather `git ls-files` if git is available (works with depth 1), plus the repo root.
  4. Gzip and POST multipart to `/api/v1/upload`.
  5. Auth: `--token` / `COVERAGE_TOKEN`, else try GitHub OIDC automatically if `ACTIONS_ID_TOKEN_REQUEST_URL` is set.
  6. Options: `--flag backend`, `--name`, `--fail-on-error` (default false, so coverage infra never breaks builds; mirrors Codecov's `fail_ci_if_error`).
- Optionally parse client-side? **No.** Upload raw reports, so parser fixes apply retroactively and the CLI stays thin. Exception: if very large, the CLI may pre-filter.
- **curl-only fallback** (documented): `curl -fsS -H "Authorization: Bearer $COVERAGE_TOKEN" -F "sha=$SHA" -F "slug=my-org/x" -F "flags=frontend" -F "file=@coverage/lcov.info" https://coverage.example.com/api/v1/upload`

### 7.3 GitHub composite action (`<owner>/code-coverage/action`)

```yaml
name: Upload coverage
inputs:
  files: { required: false }
  flags: { required: false }
  token: { required: false }            # optional; OIDC used when id-token: write
  url:   { required: true }
runs:
  using: composite
  steps:
    - shell: bash
      run: dotnet tool update -g CodeCoverage.Uploader --version 1.* || true
    - shell: bash
      env:
        COVERAGE_TOKEN: ${{ inputs.token }}
        COVERAGE_URL: ${{ inputs.url }}
      run: coverage-upload --files "${{ inputs.files }}" --flags "${{ inputs.flags }}"
```

Usage:

```yaml
permissions: { contents: read, id-token: write }
steps:
  - uses: actions/checkout@v4
  - run: dotnet test --collect:"XPlat Code Coverage"
  - uses: my-org/code-coverage/action@v1
    with: { url: https://coverage.example.com, flags: backend }
```

The action must live in a repo the other private repos can use. Under Settings → Actions → "Access", allow access from repositories in the org. On Windows runners `shell: bash` works (Git Bash), or use `pwsh`.

### 7.4 Azure Pipelines snippet (GitHub-hosted repo)

```yaml
steps:
- checkout: self
  fetchDepth: 1                          # fine: diff is computed server-side
- task: DotNetCoreCLI@2
  inputs: { command: test, arguments: '--collect:"XPlat Code Coverage"' }
- script: |
    dotnet tool install -g CodeCoverage.Uploader || true
    coverage-upload --flags backend --files "$(Agent.TempDirectory)/**/coverage.cobertura.xml"
  displayName: Upload coverage
  condition: succeededOrFailed()
  env:
    COVERAGE_TOKEN: $(COVERAGE_TOKEN)    # secret variable / variable group
    COVERAGE_URL: https://coverage.example.com
```

- Ship it as a reusable **template** in a shared templates repo (`resources.repositories` + `template: coverage-upload.yml@templates`).
- The status/comment goes to GitHub through the **GitHub App** on the server, so Azure needs no GitHub credentials. That solves "Azure Pipelines builds of GitHub repos must report to GitHub".
- Use `SYSTEM_PULLREQUEST_SOURCECOMMITID` for PR builds (Azure builds the merge commit).

---

## 8. Config file (codecov.yml compatibility)

- Read `.coverage.yml` → `codecov.yml` → `.codecov.yml` → `.github/codecov.yml` from the repo at the head sha (`contents` API). Fall back to an org-level default stored in the DB.
- Parse with **YamlDotNet** (16.x, MIT).
- Support a Codecov-compatible subset to ease migration:
  - `coverage.status.project.default.{target: auto|NN%, threshold, base: auto, flags, paths, informational, if_ci_failed}`
  - `coverage.status.patch.default.{target, threshold}`
  - `comment.{layout, require_changes, after_n_builds, behavior}`
  - `flags.<name>.{paths, carryforward}`
  - Codecov's newer equivalent of flags, `component_management`, for v2
  - `ignore`, `fixes`, `coverage.range`
- `target: auto` = base coverage (project) with `threshold` allowed drop.

## 9. Suggested solution layout

```
src/Coverage.Core        (model, parsers, path fixer, merge, diff hunk parser, yaml config)  -> no ASP.NET deps, heavily unit-tested
src/Coverage.Server      (minimal APIs, Blazor SSR UI, EF Core DbContext, GitHub App client, webhooks, workers, badges)
src/Coverage.Uploader    (dotnet tool, CI env detection, multipart upload)
tests/Coverage.Core.Tests  (golden files per format from coverlet/jest/vitest/jacoco/istanbul-clover; ReportGenerator as oracle)
tests/Coverage.Server.Tests (Testcontainers.PostgreSql 4.x + WebApplicationFactory; WireMock.Net for GitHub API)
action/                  (composite GitHub Action)
azure/                   (pipeline template)
```

## Sources
- [Octokit on NuGet (14.0.0)](https://www.nuget.org/packages/Octokit)
- [Octokit.Webhooks.AspNetCore on NuGet (4.2.3)](https://www.nuget.org/packages/Octokit.Webhooks.AspNetCore)
- [AspNet.Security.OAuth.GitHub on NuGet (10.0.0)](https://www.nuget.org/packages/AspNet.Security.OAuth.GitHub)
- [GitHub REST: list pull request files (3000-file cap)](https://docs.github.com/en/rest/pulls/pulls#list-pull-requests-files)
- [GitHub REST: compare two commits (300 files, first page only)](https://docs.github.com/en/rest/commits/commits#compare-two-commits)
- [OpenJDK Skara thread on missing `patch` with per_page > 70](https://mail.openjdk.org/pipermail/skara-dev/2022-April/005906.html)
- [augment-agent issue on 422 too_large for diffs over 300 files](https://github.com/augmentcode/augment-agent/issues/16)
- [GitHub Actions OIDC reference](https://docs.github.com/en/actions/reference/security/oidc)
- [Refreshing GitHub App user access tokens (8 h / 6 months)](https://docs.github.com/en/enterprise-server@3.17/apps/creating-github-apps/authenticating-with-a-github-app/refreshing-user-access-tokens)
- [ReportGenerator.Core on NuGet](https://www.nuget.org/packages/ReportGenerator.core)
- [ReportGenerator repository (Apache-2.0)](https://www.github.com/danielpalme/ReportGenerator)
- [Npgsql.EntityFrameworkCore.PostgreSQL on NuGet (10.0.1)](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL)
- [System.CommandLine 2.0.10 on NuGet](https://nuget.org/packages/System.CommandLine/2.0.10)
- [Azure Pipelines Sprint 240 update (Oidctoken API, System.OidcRequestUri)](https://learn.microsoft.com/azure/devops/release-notes/2024/pipelines/sprint-240-update#features)
- [Azure DevOps OIDC issuer/subject format (Databricks doc)](https://learn.microsoft.com/azure/databricks/dev-tools/auth/provider-azure-devops)

Not verified against official documentation in this research:
- The exact per-file `patch` size threshold.
- Whether the `per_page>70` patch omission still occurs.
- The exact license field inside the ReportGenerator.Core 5.5.x nupkg.
- Whether Octokit has a release newer than 14.0.0. nuget.org shows none.
