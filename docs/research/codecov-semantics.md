# How Codecov works: reference for a .NET self-hosted reimplementation

**Sources.** Most of this comes from reading the open-source code. I made shallow clones of `codecov/umbrella`, the current monorepo that now holds `apps/codecov-api`, `apps/worker` and `libs/shared` (the old `codecov-api`, `worker` and `shared` repos were merged into it), plus `codecov/codecov-cli` and `codecov/self-hosted`. I also checked docs.codecov.com. File paths below are relative to those repos. Where the code and the docs disagree, the code wins.

---

## 1. Upload protocol

### 1.1 Current protocol (codecov-cli, "v3"/ingest)
- Base URL is `https://ingest.codecov.io`, or `--enterprise-url` for self-hosted. API calls (results) go to `https://api.codecov.io`.
- The slug is encoded as `owner::::repo`. GitLab subgroups use `:::` between owner segments (`helpers/encoder.py`).
- `service` is `github`, `gitlab`, `bitbucket`, `github_enterprise`, and so on.
- Auth header is `Authorization: token <TOKEN>`. It can be omitted (see tokens below).

**Step-by-step mode** (`create-commit`, `create-report`, `do-upload`):
1. `POST /upload/{service}/{slug}/commits` with JSON `{commitid, parent_commit_id, pullid, branch, cli_args, version}`.
2. `POST /upload/{service}/{slug}/commits/{sha}/reports` with JSON `{code: "default", cli_args, version}`. A "report" is a per-commit container; `code` is almost always `default`.
3. `POST /upload/{service}/{slug}/commits/{sha}/reports/{code}/uploads` with JSON `{ci_service, ci_url, cli_args, env, flags:[..], job_code, name, version, file_not_found}`.
   - The response is `{external_id, created_at, raw_upload_location, state, provider, upload_type, url, ...}`.
   - `raw_upload_location` is a presigned S3/MinIO PUT URL.
4. `PUT <raw_upload_location>` with the payload body.

**Single-call mode** (`upload-coverage`, the default since 2024):
- `POST /upload/{service}/{slug}/upload-coverage` carries everything from steps 1–3 in one JSON body: `branch, code, commitid, parent_commit_id, pullid, flags, name, job_code, ci_url, ci_service, env`.
- It returns `{raw_upload_location, url}`, followed by the same PUT.

Other endpoints:
- `POST .../commits/{sha}/upload-complete` is a manual trigger: notify now (used with `codecov.notify.manual_trigger`).
- `POST .../commits/{sha}/empty-upload` is for commits that only touch non-testable files. It sets passing statuses.

**Payload stored in object storage** (JSON, `upload_sender.py`):
```json
{
  "report_fixes": {"format":"legacy","value": {"<path>": {"eof": 123, "lines": [1,5,9]}}},
  "network_files": ["src/a.cs", "web/src/b.ts", "..."],
  "coverage_files": [{"filename":"coverage.cobertura.xml","format":"base64+compressed","data":"<base64(zlib(content))>","labels":""}],
  "metadata": {}
}
```
- `network_files` is the output of `git ls-files -z` from the repo root. It can be filtered with `--network-filter` and given a prefix with `--network-prefix`. A shallow clone does not affect it.
- `report_fixes` are lines the CLI flags as non-executable (blank lines, braces, comments) so the server drops them. This is optional; you can skip it.

### 1.2 Legacy protocol (bash uploader / v4): the simplest one for `curl`
- `POST https://codecov.io/upload/v4?commit=&branch=&build=&build_url=&name=&slug=owner/repo&service=&flags=a,b&pr=&job=&package=`
- Header: `X-Upload-Token: <token>`.
- The response body is two lines: `result_url\nput_url`. The client then PUTs a plain-text body:
```
<env vars>\n<<<<<< ENV
src/a.cs\nsrc/b.ts\n<<<<<< network
# path=coverage.xml
<raw file content>
<<<<<< EOF
```
- **Recommendation:** for Azure Pipelines and plain shells, support a single-request variant of this (multipart or this text format) so a `curl` one-liner works. Add a `dotnet tool` that does the same thing with auto-detection.

### 1.3 Tokens (`codecov_auth/authentication/repo_auth.py`)
The server tries these authentication classes in order:
- **Repository upload token** (`Repository.upload_token`, a UUID). This is the per-repo `CODECOV_TOKEN`.
- **Org-level token** (`OrganizationLevelToken`, a UUID, optional `valid_until`). One token for every repo in the org.
  - **This is the one to copy for the served organisation (`<owner>`)**: one secret shared by all pipelines.
- **Global upload token** (site config, mapped to a service). It works for any repo of that service; the repo is resolved from the path.
- **GitHub Actions OIDC token.** A JWT from GitHub's OIDC provider, verified against the repo. Uploads need no secret.
  - Worth implementing for GitHub Actions: check `iss=https://token.actions.githubusercontent.com`, `aud`, and the `repository` claim.
- **Tokenless.** Only for **public** repos, and only when the branch contains `:` (a fork, `forkowner:branch`). An org setting `upload_token_required_for_public_repos=false` allows tokenless uploads for all public repos.
  - Not relevant for private repos.

### 1.4 Commit / PR / branch detection in the CLI (important for us)
- **GitHub Actions** (`ci_adapters/github_actions.py`):
  - Branch is `GITHUB_HEAD_REF` or `GITHUB_REF` without `refs/heads/`. Merge-queue branches `gh-readonly-queue/<b>/...` map to `<b>`.
  - PR number is parsed from `refs/pull/(\d+)/merge`.
  - On PR events, `GITHUB_SHA` is the **synthetic merge commit**. The CLI runs `git rev-parse HEAD^@` and takes the 2nd parent as the real head.
  - **With `fetch-depth: 1` this fails silently and the merge-commit SHA is uploaded.** Statuses then land on a commit that is not the PR head, so they never show on the PR.
  - Fix options: our uploader reads `github.event.pull_request.head.sha` from `GITHUB_EVENT_PATH`, which works with shallow clones, or we use fetch-depth 2.
- **Azure Pipelines** (`azure_pipelines.py`):
  - SHA is `SYSTEM_PULLREQUEST_SOURCECOMMITID`, falling back to `BUILD_SOURCEVERSION`. On PR builds `BUILD_SOURCEVERSION` is the merge commit, so the source commit id is essential.
  - PR is `SYSTEM_PULLREQUEST_PULLREQUESTNUMBER` (GitHub PR number), falling back to `..._PULLREQUESTID`.
  - Branch is `BUILD_SOURCEBRANCH` without `refs/heads/`. On PR builds this is `refs/pull/N/merge`; use `SYSTEM_PULLREQUEST_SOURCEBRANCH` instead.
  - Slug is `BUILD_REPOSITORY_NAME`, which is `owner/repo` for GitHub-hosted repos.
  - Build is `BUILD_BUILDNUMBER`, job is `BUILD_BUILDID`. Build URL is `{SYSTEM_TEAMFOUNDATIONCOLLECTIONURI}{SYSTEM_TEAMPROJECT}/_build/results?buildId={BUILD_BUILDID}`.
- **The diff is computed server-side** through the GitHub compare API, so shallow CI clones never affect patch coverage. Only SHA detection is affected.

### 1.5 Coverage file discovery (CLI)
- Default globs include `*coverage*.*`, `cobertura.xml`, `*.lcov`, `lcov.info`, `jacoco*.xml`, `*Jacoco*.xml`, `clover.xml`, `*.clover`, `coverage-final.json`, `codecov.*` and others.
- `vendor`, `node_modules` and similar folders are excluded.
- `-f` gives explicit files; `--disable-search` turns discovery off.

### 1.6 How multiple uploads for one commit merge
- Each upload becomes one **session** (integer id, incrementing) in the commit's single merged report.
- Processing (worker `upload_processor` → `upload_finisher`):
  - Each upload is parsed into an intermediate report under a per-commit lock, then merged into the master report.
  - Line-level merge: for each line, `sessions` is the union of per-session entries, and the line's `coverage` is `merge_all(session coverages)`. See section 2.
- **Uploads accumulate.** A CI re-run adds another session with the same flag. Because line merge takes the max, duplicates do no harm.
- The exception is **carryforward sessions**. When an upload with flag F arrives, every `carriedforward`-type session carrying F is deleted first (`clear_carryforward_sessions`, `services/processing/merging.py`).
- When notifications fire (`upload_finisher.should_call_notifications`):
  - Not while any upload of the commit is still in state `UPLOADED` (not yet processed).
  - Not if `codecov.notify.manual_trigger` is set.
  - Not if `codecov.notify.after_n_builds > len(report.sessions)`.
  - Not if no processing succeeded. With `notify_error: true`, any failure produces an error notification.
  - Then notify runs. Status payloads are cached for 10 minutes to avoid re-posting identical statuses.
- `codecov.require_ci_to_pass` (default **true**) plus `codecov.notify.wait_for_ci` (default **true**):
  - Codecov reads the commit's GitHub statuses/checks (the `status` webhook) to compute `commit.ci_passed`.
  - It waits for CI to finish before notifying.
  - If CI failed, every existing codecov status is set to state `if_ci_failed` (default `error`) with "Coverage not measured fully because CI failed".
  - **For a small reimplementation:** default `wait_for_ci` to false and rely on all-uploads-processed, `after_n_builds`, or an explicit "done" call. Polling GitHub CI state across Azure and GitHub Actions is the most complex and least valuable part.

---

## 2. Report data model (`libs/shared/shared/reports/*`, `utils/merge.py`)

**Report**
- `files`: `path → ReportFile`
- `sessions`: `id → Session`
- `totals`: `ReportTotals`

**Session** (one per upload)
- `id`, `flags[]`, `provider` (CI service), `build`, `job`, `url` (CI URL), `name`, `env`, `time`, `totals`
- `session_type`: `uploaded` | `carriedforward`
- `session_extras`: `{carriedforward_from: <sha>}` for CF sessions

**ReportFile**: `name`, `lines` (`ln → ReportLine`), `totals`.

**ReportLine**
- `coverage`, `type` (`None`/`line`, `b` = branch, `m` = method), `sessions: [LineSession]`, `complexity`.

**LineSession**: `(session_id, coverage, branches_missing[]?, partials?, complexity?)`.

**Values a `coverage` field can take** (`line_type()`):

| value | meaning |
|---|---|
| int `> 0` | hit |
| int `0` | miss |
| str `"c/t"` | branch: `c == t` is a hit, `c == "0"` is a miss, otherwise a **partial** |
| `True` | partial |
| `-1` | skipped (fixed out) |
| `None`/`False` | ignored |

**Line merge across sessions** (`merge_coverage`):
- int vs int: take the max.
- branch vs branch: the missing branches are the **intersection** of each session's missing-branch lists, giving `(total - len(missing))/total`. Without that info, take the "greatest" `a/b`.
- int vs branch: if the int is > 0, the line counts as fully covered (`t/t`). If the int is 0, keep the branch value.
- `-1` wins (ignored).
- Simple and faithful rule for us: store per session `hits` and `branchesCovered/branchesTotal` plus the list of missing branch ids. Merged value = union of covered branches; hit if any session hit.

**ReportTotals fields**
- `files, lines, hits, misses, partials, coverage (string, 5 decimals), branches, methods, messages, sessions, complexity, complexity_total, diff`.
- In the DB `commit.totals` they are short keys: `f,n,h,m,p,c,b,d,M,s,C,N,diff`.

**Flags**
- A flag is a label on a session.
- A flag's view of a report = files/lines restricted to sessions with that flag (`report.filter(flags=..)`), combined with the flag's `paths` filter from YAML.
- With `flags.<name>.joined: false` (legacy, rare), that flag's sessions are stored but not merged into the line coverage.

**Parser semantics for our five formats** (`services/report/languages/`):
- **Cobertura** (root `<coverage>`):
  - Every `<class filename>` → file; every `<line number hits>` → line.
  - If `branch="true"` and `condition-coverage="50% (1/2)"`, coverage is `"1/2"` with type branch. Otherwise it is `int(hits)`.
  - Missing branches come from the `<condition>` children whose coverage is not 100%.
  - `<source>` entries that start with `/` are tried as path prefixes.
  - Reports with a `timestamp` older than `codecov.max_report_age` (default `"12h ago"`) are rejected.
  - Options: `parsers.cobertura.partials_as_hits`, `handle_missing_conditions`.
- **OpenCover** (root `<CoverageSession>`, `csharp.py`):
  - `File uid → fullPath`.
  - For each `Method`'s `SequencePoint`: if `bec != "0"` the coverage is `"{bev}/{bec}"` (branch). Otherwise it is `vc`.
  - Multi-line sequence points (`el > sl`) apply to every line in `sl..el`.
  - `BranchPoint` entries with `vc="0"` give the missing branches, keyed by `sl`.
- **LCOV** (contains `\nend_of_record`):
  - `SF:` starts a file. `DA:ln,count` gives line hits.
  - `BRDA:ln,block,branch,taken`: each branch counts 1 if taken is not `-` or `0`, giving `"sum/num"`.
  - `FN` lines become method type.
  - `BRDA` on line 1 of `.ts` files is skipped.
  - `TN, LF, LH, FNF, FNH, BRF, BRH, FNDA` are ignored.
  - Option: `parsers.lcov.partials_as_hits`.
- **JaCoCo** (root `<report>`):
  - Path is `package@name/sourcefile@name`.
  - Each `<line nr mi ci mb cb>`: if `mb != 0` the coverage is `"cb/(mb+cb)"`; if `cb != 0` it is `"cb/cb"`; otherwise it is `ci` (covered instructions as the hit count).
- **Clover** (root `<coverage generated=...>`):
  - `file@path` or `@name`.
  - `<line type="cond" truecount falsecount>` gives `0/2`, `1/2` or `2/2`. `type="method"` and `type="stmt"` use `count`.

---

## 3. Coverage math

### 3.1 Coverage percentage
In `shared/reports/totals.py` and `helpers/numeric.ratio`:
```
lines    = hits + misses + partials          (ignored/skipped lines excluded)
coverage = hits / lines * 100               (string, rounded to 5 decimals; "100" if equal, "0" if hits==0)
```
- **Partials count in the denominator and never in the numerator.** A line with 1 of 2 branches covered counts as 0 hit, 1 partial. It is not 50%.
- Branch counts do not enter the formula. `branches` is only an informational counter.
- Patch coverage, flag coverage and project coverage all use the same formula.
- Display rounding is `coverage.precision` (default **2**) and `coverage.round` (default **down**; the worker fallback when unset is `nearest`).
- Comparisons for statuses use unrounded Decimals.

### 3.2 Project coverage
This is the totals of the whole HEAD report, after `ignore` and path fixing, and filtered by the status's `flags`/`paths`. It is compared to the same filter applied to the BASE report.

### 3.3 Patch coverage
In `shared/reports/diff.py`:
- Take the git diff from base to head, fetched server-side through the GitHub compare API (`get_compare(base, head)`).
- For every file of type `new` or `modified`, walk each hunk. The relevant lines are the **added** (`+`) lines, numbered in the new file. The new-file counter starts at `header[2]` (the new start) and skips `-` lines.
- Look up each added line in the HEAD report and sum the totals of those that exist (coverable lines only). Patch coverage = `hits / (hits + misses + partials)`.
- If no coverable lines changed (`lines == 0`), patch coverage is `None`. The patch status then reports "Coverage not affected" and passes.
- Patch coverage uses the **original PR base** (`pull.base`, `use_original_base=True`). Project comparison uses the adjusted base (`pull.compared_to`). See 3.5.

### 3.4 Indirect changes ("unexpected changes")
In `worker/services/comparison/changes.py` `get_changes(base_report, head_report, diff)`:
- For each file in HEAD that is neither new nor deleted:
  - Map lines from base to head through the diff offsets.
  - Compare per-line coverage type (hit/miss/partial) **outside the lines touched by the diff**.
  - Any difference gives a `Change(path, in_diff, totals=diff of h/m/p, coverage delta)`.
- **Missing files** (in base, not in head, not explained by the diff) and **new files** (in head, not in base, not in the diff) are also listed.
- The `codecov/changes` status fails when a non-new, non-deleted change has `misses + partials > 0`. Message: "N files have indirect coverage changes not visible in diff".
- In the comment: the files table links "N files with indirect coverage changes".

### 3.5 Base commit selection for a PR
**Step 1, find `pull.base`.** In `torngit/github.py get_pull_request`:
- Start from GitHub's `base.sha`.
- Walk the PR's commit list from the head through parents until reaching parents that are **not** in the PR. Those are the fork point, in effect the merge-base. If that differs from GitHub's `base.sha`, use it.
- A user-provided base SHA is tried first if present.

**Step 2, find `pull.compared_to`.** In `_pick_best_base_comparedto_pair`, worker `services/repository.py`:
- If `pull.base` has a commit row (a Codecov report) in the DB, use it.
- Otherwise use a **pseudo-base**: the most recent commit on the PR's base branch with `(pullid IS NULL OR merged)` and `timestamp < base.timestamp`, filtered by `ci_passed` when `require_ci_to_pass`.
- Otherwise there is no base, and statuses use `if_not_found`.

Both values are used:
- Project status, the diff table and the comment header compare HEAD to `compared_to`.
- Patch coverage uses the diff `pull.base..head`.

**Non-PR commit** (push to a branch):
- The base is the **parent** commit (`commit.parent_commit_id`), resolved by `fetch_appropriate_parent_for_commit`.
- That function walks GitHub ancestors until it finds a commit Codecov knows. It prefers commits with a message, and the same branch when several match.
- The patch is then the diff of that single commit against the parent.

### 3.6 Status algorithms (`notifiers/mixins/status.py`)
**Project status**
- If there is no head report, or no head coverage: state = `if_not_found` (default success), "No coverage information found on head".
- If there is no base report, or no base coverage: state = `if_not_found`, "No report found to compare against".
- `target = YAML target` when numeric, else `base.coverage` (`auto`). `threshold` defaults to `0`.
- `success` if `head >= target - threshold`, else `failure`.
- Messages:
  - auto target: `"{head}% ({head-base:+}%) compared to {base7}"`
  - custom target: `"{head}% (target {t}%)"`
- If the result is a failure, apply `removed_code_behavior`. The default is **`adjust_base`**, and it only applies when the target is `auto`:
  - Remove from the base totals the hits/misses/partials of lines **deleted** in the diff.
  - `adj = base_h' / (h'+m'+p') * 100 - threshold`.
  - Pass if `adj - head < 0.005`. Message suffix: ", passed because coverage increased by X% when compared to adjusted base (Y%)".
  - Other values:
    - `removals_only`: pass if the change only removed code, with no additions and no unexpected changes.
    - `fully_covered_patch`: pass if there are no indirect changes and the patch is 100% covered or has no coverable lines.
    - `off` / `false`: no rescue.

**Patch status**
- `target = YAML target`, else **base project coverage** (`auto` means the patch must be at least as covered as the base project). Default threshold is 0.
- If the patch has coverable lines:
  - Target unknown (no base): `if_not_found`, "No report found to compare against".
  - Otherwise `success` if `cov >= target`, or `cov >= target - threshold` with the message "(within T% threshold of X%)".
  - Message: `"{cov}% of diff hit (target {t}%)"`.
- No coverable lines: success, `"Coverage not affected when comparing {base7}...{head7}"`.

**All statuses**
- `informational: true` forces the state sent to GitHub to `success`, but keeps the message.
- `only_pulls` / `base: pr`: no status for non-PR commits (merge-queue branches are allowed).
- `branches`: the status is only set for matching head branches.
- Per-flag `after_n_builds`: the status is not sent until that many sessions with the flag exist.
- `flag_coverage_not_uploaded_behavior`, set per status or in `coverage.status.default_rules`, default `include`. It applies when none of the status's flags got a real upload on this commit (carried-forward only):
  - `include`: compute normally.
  - `exclude`: send no status.
  - `pass`: force success and append " [Auto passed due to carriedforward or missing coverage]".
- Empty upload: "Non-testable files changed." (success) or "Testable files changed" (failure).

---

## 4. codecov.yml: the subset that matters

**Where it is read and how it merges**
- The worker reads YAML from the **head commit** first: `codecov.yml`, `.codecov.yml`, `codecov.yaml`, `.codecov.yaml`, at the repo root, `dev/` or `.github/`.
- If not found, it uses the YAML saved from the default branch (repo-level).
- Final config = deep-merge of site defaults, then the owner/org YAML (UI), then the commit YAML (or the repo YAML).
- `codecov.strict_yaml_branch` restricts YAML reading to that branch.

**Site defaults** (`shared/config/__init__.py`, legacy, for owners created before 2024-04-30):
```yaml
codecov:
  require_ci_to_pass: true
  notify: { wait_for_ci: true }
coverage:
  precision: 2
  round: down
  range: "60...80"          # comment/UI colours; badge uses [70,100] unless repo yaml sets range
  status:
    project: true            # == {default: {}}
    patch: true
    changes: false
    default_rules: { flag_coverage_not_uploaded_behavior: include }
comment:
  layout: "reach,diff,flags,tree,reach"
  behavior: default
  show_carryforward_flags: false
slack_app: true
```
- **Newer "patch-centric" default** (owners created after 2024-04-30):
  - `status.project: false`, `status.patch: true`
  - `comment.layout: "condensed_header, flags, tree, component"`
  - `comment.hide_project_coverage: true`
  - `range: [60, 80]`
- Pick one as our default. The legacy one matches what long-time users expect.
- `status.project: true` means a single status named `default`, i.e. context `codecov/project`. A dict of named entries gives one status each. Any entry `false` or `enabled: false` is skipped.

**`coverage.status.project.<name>` / `coverage.status.patch.<name>`** (schema `shared/validation/user_schema.py`):

| field | values | default |
|---|---|---|
| `target` | `auto` \| number \| `"80%"` | `auto` |
| `threshold` | number \| `"1%"` | `0` |
| `base` | `auto` \| `pr` \| `parent` (deprecated; `pr` = only_pulls) | `auto` |
| `flags` | list of flag names | none (all) |
| `paths` | list of globs/regex; `!` prefix = exclude | none |
| `branches` | list of branch names/globs | none (all) |
| `if_ci_failed` | `success`/`failure`/`error`/`ignore` | `error` |
| `if_not_found` | `success`/`failure`/`error`/`ignore` | `success` |
| `if_no_uploads` | same | (unused/legacy) |
| `informational` | bool | `false` |
| `only_pulls` | bool | `false` |
| `removed_code_behavior` (project) | `adjust_base`/`removals_only`/`fully_covered_patch`/`off`/`false` | `adjust_base` |
| `carryforward_behavior`, `flag_coverage_not_uploaded_behavior` | `include`/`exclude`/`pass` | `include` |
| `measurement` | `line`/`statement`/`branch`/`method`/`complexity` | (rarely used) |

- Status context names: `codecov/project`, `codecov/patch`, `codecov/changes` for the `default` entry. A named entry gives `codecov/{type}/{name}`, e.g. `codecov/project/backend`.
- Flag-defined statuses (`flag_management...statuses: [{type: project, name_prefix: "x-", target, threshold, ...}]`) give `codecov/project/{name_prefix}{flag}`.

**Top-level and other keys**
- `ignore: [paths]`: files dropped at ingestion.
- `fixes: ["before/::after/", "::prefix/"]`: path rewrites (see below).
- `codecov.notify.after_n_builds` (int), `codecov.notify.wait_for_ci` (bool), `codecov.notify.manual_trigger` (bool), `codecov.notify.notify_error`.
- `codecov.require_ci_to_pass` (bool, default true).
- `codecov.branch`: overrides the default branch.
- `codecov.max_report_age`: default `"12h ago"`; `false` disables it.
- `codecov.disable_default_path_fixes`.
- `codecov.allow_pseudo_compare`.
- `parsers.{cobertura,lcov,jacoco}.partials_as_hits` (bool, default false).

**Path patterns**
- Path patterns are a prefix, a glob (`**`, `*`) or a regex (when they start with `^` or contain regex syntax).
- `!` negates.

**Path fixing** (worker `services/path_fixer`), in order:
1. Normalise: `\` becomes `/`, then strip leading `./`/`../` and relpath.
2. Apply user `fixes`:
   - `"a/b/::c/"` replaces the prefix regex `^a/b/` with `c/`. In the left side, `**` means `.*` and `*` means `[^/]+`.
   - `"::prefix"` prepends.
3. If a network list (toc) exists and default fixes are not disabled, resolve against the repo file list (`helpers/pathmap.py Tree`):
   - Match **reversed path components** case-insensitively (suffix match). With several candidates, pick the best by `difflib` ratio.
   - Require at least one common ancestor dir (`ancestors=1`).
   - **Unresolvable paths are dropped.**
   - Also retried: prefixing the coverage file's own directory (`# path=` base) and Cobertura `<source>` dirs.
4. Without a network list, remove known bad prefixes (`/home/runner/work/...`, `/Users/...`, etc.).
5. Apply the `ignore` / flag `paths` include filter.

This network-suffix match is what makes absolute Windows/Linux agent paths from coverlet (`C:\agent\_work\1\s\src\Foo.cs`) map to `src/Foo.cs`. **It is essential for us.**

**`flags` (legacy) and `flag_management` (current)**
```yaml
flag_management:
  default_rules:            # applied to every flag
    carryforward: false     # default false
    carryforward_mode: all  # all | labels (labels = test-analytics only; ignore)
    paths: [..]             # flag only "owns" these paths
    ignore: [..]
    after_n_builds: 0
    statuses: [{type: project|patch|changes, name_prefix, target, threshold, ...}]
  individual_flags:
    - name: backend         # merged over default_rules (dict update)
      paths: [src/]
      carryforward: true
      statuses: [{type: project, target: auto, threshold: 1%}]
flags:                      # legacy form, takes precedence if a flag is named here
  backend: { paths: [src/], carryforward: true, joined: true, after_n_builds: 1 }
```
- Flag names match `^[^'"]{1,1024}$`.
- `get_paths_from_flags`: if any listed flag has no `paths`, the filter is "all paths".

**Carryforward semantics** (`worker/services/report/__init__.py create_new_report_for_commit`, `shared/reports/carryforward.py`):
- When the first upload of a commit is processed and the YAML has any carryforward flag:
  - Find the parent through `commit.parent_commit_id`.
  - Walk up at most **10** ancestors until one has state `complete` or `skipped`. A `pending` ancestor without a resolved parent raises "not ready yet" and the job retries.
- Load that parent's report and keep only sessions with flags marked `carryforward: true`, plus files that match those flags' `paths`.
- Mark the kept sessions `session_type=carriedforward`, rename them `CF[n] - <name>`, and set `session_extras.carriedforward_from=<parent sha>`.
- **Shift line numbers** using the diff from the parent to the commit (`shift_lines_by_diff`).
- This becomes the starting report for the commit.
- When a real upload with flag F arrives later, every CF session carrying F is deleted, then the upload is merged in.
- Result: flags not run on this commit (e.g. frontend not rebuilt) keep their last known coverage.
- Comment `show_carryforward_flags` (default false) hides CF flags from the flags table. When shown, it adds a "Carriedforward from <sha7>" marker.

**`comment`** (see 5): `layout`, `behavior`, `require_changes`, `require_base`, `require_head`, `after_n_builds`, `show_carryforward_flags`, `hide_project_coverage`, `hide_comment_details`, `branches`. `comment: false` disables the comment.

---

## 5. PR comment, statuses, badges, GitHub App

### 5.1 Comment behaviour (`notifiers/comment/__init__.py`, `conditions.py`)
**Conditions checked before posting:**
- A PR exists and the provider can fetch it.
- The PR head equals the commit being processed (otherwise skip, so stale commits never overwrite).
- `comment.after_n_builds <= sessions`.
- `require_changes` is met.

**`require_changes`** (only for the *first* post; once `pull.commentid` exists the comment is always updated):
- `false`: always post (default).
- `true` / `"any_change"`: post if there are indirect changes **or** the patch has coverable lines.
- `"coverage_drop"`: `head - base < 0` and `|diff| >= project.threshold + 0.01`. Missing data counts as true.
- `"uncovered_patch"`: patch coverage is not 100% (and the patch has coverable lines).
- Values can be combined with `OR` / `AND` (e.g. `"coverage_drop OR uncovered_patch"`).

**`require_base` / `require_head`** are in the schema and the docs (doc defaults: base true... the docs contradict themselves), but the current worker never reads them. The comment simply shows a "Please upload report for BASE (branch@sha)" warning when the base is missing.

**`behavior`** (default `default`):
- `default`: edit the stored comment if it exists. If it was deleted (404), post a new one.
- `once`: edit if it exists. If the user deleted it, do nothing.
- `new`: delete the old comment and post a new one.
- `spammy`: always post a new one.

**The comment id is stored in the DB (`pull.commentid`).** There is no marker search. Either approach works for us; storing the id plus a hidden HTML marker fallback is the most robust.

### 5.2 Comment content
Header: `## [Codecov](<pr url>) Report`. The layout is parsed from the comma-separated `layout`:
- If no header section is listed, `condensed_header` is forced first.
- `files` / `tree` also appends a `newfiles` section.

**Header section**
- `:x: Patch coverage is \`{p}%\` with \`{misses+partials} lines\` in your changes missing coverage. Please review.`
  - or `:white_check_mark: All modified and coverable lines are covered by tests.`
- Unless `hide_project_coverage`: `:white_check_mark: Project coverage is {head}%. Comparing base ({base7}) to head ({head7}).`
- Optional warnings:
  - "Report is N commits behind head on {base branch}."
  - "Current head X differs from pull request most recent head Y".

**`diff` section**: a fenced ```diff table with columns `{base branch} | #{PR} | +/-`.
- Rows: `Coverage, Complexity, Files, Lines, Branches, Hits, Misses, Partials`.
- Line prefix `+`/`-` by good or bad direction (misses going up is `+` = bad).

**`files` / `tree` section**: `| Files with missing lines | Coverage Δ | |`.
- Each row: `[path](link) \`{head%} <{patch%}> ({Δ vs base:+}%)\``.
- Deleted files are struck through.
- Sorted by missing lines; default limit 10 (`files:20` changes it), then "... and N more".
- Then "... and N files with indirect coverage changes".

**`newfiles` / `condensed_files`**: only files whose patch has missing lines, as `| Patch % | Lines |`.

**`flags` section**: table `| Flag | Coverage Δ | |` per flag (base vs head), including the carryforward marker.

**`footer`**
- Legend: `Δ = absolute <relative> (impact)`, `ø = not affected`, `? = missing data`.
- "Last update {base7}...{head7}".

**Number formatting**
- `format_number_to_str` uses precision and round.
- A non-zero value that rounds to 0 is shown as `<0.01` / `+<0.01`.
- Null is `∅`; zero impact is `ø`.

### 5.3 Status checks
- They are posted through the **Commit Status API**: `POST /repos/{o}/{r}/statuses/{sha}` with `{state, context, description, target_url}`. `target_url` is the PR page or the commit page.
- When the repo is covered by the GitHub App installation and `github_checks` is not `false`, Codecov uses the **Checks API** instead (with fallback to statuses). Checks can add line annotations (`github_checks.annotations`).
- **For us, plain commit statuses are enough.** They also work for Azure-built commits, since the status is attached to the SHA. Make them required in branch protection by context name.
- Before posting, Codecov reads the existing statuses and skips identical state and description.

### 5.4 Badges and graphs (`codecov-api/graphs/urls.py`, `views.py`)
**URL scheme**
- `/{service}/{owner}/{repo}/graph/badge.svg` (default branch)
- `/{service}/{owner}/{repo}/branch/{branch}/graph/badge.svg`
- `service` can be `gh` or `github`. `graphs` also works in place of `graph`.
- Extensions: `.svg` or `.txt` (just the number).

**Query parameters**
- `token=<repo image_token>`: **required for private repos**. This is a separate read-only "graphing token", not the upload token.
- `flag=<name>`, `component=<id>`.
- `precision=0|1|2` (default 0).

**Value and colour**
- The value is the `totals.c` of the branch's head commit (`Branch.head`). An unknown repo, branch or token gives a grey "unknown" badge, never a 404.
- Colour comes from `coverage_to_color(low, high)` over `range`. The badge default is `[70,100]` unless the repo YAML has `coverage.range`. The 101-colour gradient runs `#e05d44 → #fe7d37 → #dfb317 → #a4a61d → #97CA00 → #4c1`:
  - `≤ low` is red; `≥ high` or 100 is bright green.
  - Otherwise the index is `int((cov - low) / (high - low) * 100)`.

**Other graphs**: `.../graph/{tree|icicle|sunburst|commits}.svg` (optional; skip).

**Coverage history**
- Each commit row stores `totals` and a timestamp; branches store `head`. History is a time series of commit totals on a branch.
- Self-hosted adds TimescaleDB for `timeseries` (measurements per flag/component). It is disabled by default (`setup.timeseries.enabled: false`).
- **For us:** a `commit_totals` table plus a `(repo, branch, timestamp)` index, with a per-flag totals table, is enough.

### 5.5 GitHub App: permissions and webhooks
Codecov's public pages show the permissions only in screenshots. The list below is **derived from the API calls the code makes**, so double-check it when creating the app:

**Repository permissions**
- Contents: Read. Needed for the compare/diff API, fetching `codecov.yml`, and commit info/ancestors.
- Metadata: Read (mandatory).
- Commit statuses: Read & write.
- Pull requests: Read & write. This also allows issue comments on PRs; alternatively Issues: R/W.
- Checks: Read & write (only if we implement Checks).

**Organization permissions**: Members: Read (Codecov uses it for login/team sync; not needed for us).

**Account permissions**: Email addresses: Read (login only; the self-hosted docs say "All others can be set to 'No Access'").

**Webhook events handled** (`webhook_handlers/constants.py`):
- `pull_request` (opened, synchronize, reopened, closed, edited, labeled, ready_for_review)
- `push`, `delete`, `status`, `repository`, `public`, `member`, `installation`, `installation_repositories`, `organization`
- Webhook URL: `/webhooks/github` (`/webhooks/github_enterprise` on self-hosted). It is signed with the webhook secret.

**Minimum for us**
- `pull_request`: keeps PR ↔ base/head mapping and triggers re-notification when the base changes.
- `push`: sets the default-branch head, used for badges and history.
- `installation*`: discovers repos.
- Optionally `status` / `check_suite`, only if we implement `wait_for_ci`.

**Authentication**
- Use an App JWT (RS256 with the PEM) to get an installation access token (`POST /app/installations/{id}/access_tokens`), and cache it for about 1 hour.
- This works for builds from Azure Pipelines as well, since the server is the one talking to GitHub.

---

## 6. Self-hosted architecture: what to keep and what to drop

**Codecov self-hosted** (`self-hosted/docker-compose.yml`) runs 8 containers:
- `gateway`: nginx routing to api, frontend and minio.
- `frontend`: React SPA.
- `api`: Django REST + GraphQL, which also hosts upload endpoints, webhooks and badges.
- `worker`: Celery; tasks upload → upload_processor → upload_finisher → notify, plus sync_pull, sync_repos, timeseries.
- `redis`: Celery broker, locks, caches.
- `postgres`: main DB.
- `timescale`: TimescaleDB for time series.
- `minio`: S3 store for raw uploads and chunked report files (`chunks.txt` and `files`/`sessions` JSON per commit).
- Config lives in `/config/codecov.yml` (site settings, GitHub app id/PEM/webhook secret, `setup.timeseries`, a global upload token, and so on).
- Some parts of the report processing are in Rust (`cc_rustyribs`, `rust_analyzer`) for performance.

**Overkill for a small team (drop or collapse):**
- **Redis + Celery.** Use an in-process `BackgroundService` with a Postgres-backed job queue (`SELECT ... FOR UPDATE SKIP LOCKED`), or `System.Threading.Channels` plus a DB "pending uploads" table so restarts are safe. Use per-commit serialisation (`pg_advisory_xact_lock(hash(repo, sha))`) instead of Redis locks.
- **MinIO.** Store raw uploads compressed (gzip `bytea`) in Postgres, or on a mounted volume / Azure Blob through one storage interface. Store the merged per-commit report as compressed JSON or a compact binary blob in a `commit_reports` row.
- **TimescaleDB.** Plain Postgres tables (`commit_totals`, `commit_flag_totals`) are enough for history charts at this scale.
- **Gateway / separate frontend.** Serve everything from one ASP.NET Core app (Minimal API + Razor/Blazor or a static SPA).
- **The GraphQL API, Rust analysers, plan/billing/seat logic, marketplace, Slack/IRC/Gitter/email notifiers, bundle analysis, test analytics, components, ATS labels, the `reach` sunburst graphs, and OAuth user login sync.** All can go.
  - For UI auth, GitHub OAuth plus an org-membership check, or Entra ID, is enough.

**Worth keeping semantically:**
- The upload endpoint shape: accept both a CLI-style JSON and a single curl-friendly multipart/text request.
- Per-commit merged report with sessions and flags.
- Network-file path fixing.
- Carryforward with parent walk and line shifting.
- `pull.base` (fork point) and `compared_to` (pseudo-base).
- Server-side diff through the GitHub compare API.
- The exact coverage formula (partials in the denominator).
- Project and patch statuses with target/threshold, `adjust_base` and `informational`.
- Single updated PR comment.
- `after_n_builds`.
- Badges with an image token.

**Suggested target**: 1 container (ASP.NET Core 10: API, background worker, UI) and 1 Postgres. A GitHub App supplies the identity for statuses and comments. Uploaders:
- a `dotnet tool` (auto-detects Azure Pipelines and GitHub Actions env vars, runs `git ls-files`, posts all reports in one request),
- a curl recipe,
- a composite GitHub Action that runs the tool and uses an OIDC token.

**Implementation notes specific to this environment**
- **Shallow clones:** compute the diff server-side (GitHub compare API from `pull.base` to head) and never in CI.
  - In the uploader, get the PR head SHA from `SYSTEM_PULLREQUEST_SOURCECOMMITID` (Azure) or from `GITHUB_EVENT_PATH` → `pull_request.head.sha` (GHA). Do not use the merge commit.
  - The parent commit for a push build can come from the GitHub API on the server (`GET /commits/{sha}` → parents) rather than from the clone.
- **Monorepo (.NET + JS):** one upload per stack, with `flags=backend` / `flags=frontend` and flag `paths` (`src/Api/`, `web/`).
  - Use `carryforward: true` so a PR that only builds the frontend keeps backend coverage.
  - Use `codecov.notify.after_n_builds: 2`, or per-flag `after_n_builds`, so statuses are not posted after the first upload.
- **coverlet output:** prefer Cobertura or OpenCover. Multi-target builds produce several files per project; they merge naturally as sessions/lines.
  - If you upload reportgenerator's merged Cobertura, paths are already relative, but the network list still normalises them.

---

## Main source files (in the shallow clones)
- **Upload client:** `codecov-cli/codecov_cli/services/upload/upload_sender.py`, `legacy_upload_sender.py`, `helpers/ci_adapters/{github_actions,azure_pipelines}.py`, `services/upload/{file_finder,network_finder}.py`
- **Upload server and auth:** `umbrella/apps/codecov-api/upload/urls.py`, `upload/serializers.py`, `upload/views/upload_coverage.py`, `codecov_auth/authentication/repo_auth.py`
- **Data model and math:** `umbrella/libs/shared/shared/reports/{types,totals,diff,carryforward,resources}.py`, `utils/merge.py`, `helpers/numeric.py`
- **YAML:** `umbrella/libs/shared/shared/config/__init__.py` (defaults), `validation/user_schema.py` (full schema), `yaml/user_yaml.py` (merge order, flag config)
- **Comparison and base selection:** `umbrella/apps/worker/services/comparison/{__init__,changes}.py`, `services/repository.py`, `libs/shared/shared/torngit/github.py` (`get_pull_request`)
- **Statuses and comment:** `umbrella/apps/worker/services/notification/notifiers/mixins/status.py`, `status/base.py`, `comment/{__init__,conditions}.py`, `mixins/message/{__init__,sections,helpers}.py`
- **Carryforward, merging, notify trigger:** `umbrella/apps/worker/services/report/__init__.py`, `services/processing/merging.py`, `tasks/upload_finisher.py`
- **Parsers:** `umbrella/apps/worker/services/report/languages/{cobertura,csharp,lcov,jacoco,clover}.py`
- **Path fixing:** `umbrella/apps/worker/services/path_fixer/*`, `helpers/pathmap.py`
- **Badges:** `umbrella/apps/codecov-api/graphs/{urls,views}.py`, `graphs/helpers/badge.py`, `libs/shared/shared/helpers/color.py`
- **Self-hosted layout:** `self-hosted/docker-compose.yml`

## Web sources
- https://docs.codecov.com/docs/commit-status
- https://docs.codecov.com/docs/pull-request-comments
- https://docs.codecov.com/docs/carryforward-flags
- https://docs.codecov.com/docs/codecov-yaml
- https://docs.codecov.com/docs/how-to-create-a-github-app-for-codecov-enterprise (callback `/login/ghe`, webhook `/webhooks/github_enterprise`; the permissions appear only in screenshots)
- https://docs.codecov.com/do/docs/integrations-for-codecov-self-hosted
- https://github.com/apps/codecov

All clones are under `C:\Users\GTHOUV~1\AppData\Local\Temp\claude\94735b31cef3ecd8\81bdc825-3b75-4e7d-ab39-c40a36fc0ade\scratchpad\src\` (`umbrella`, `codecov-cli`, `self-hosted`).
