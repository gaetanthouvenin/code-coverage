# Azure DevOps diff measurement (spike M7.01)

`Measure-AzureDevOpsDiff.ps1` measures, on real pull requests of an Azure DevOps test organization,
the three ways of getting the changed lines of a pull request that
[docs/research/azure-devops-diff.md](../../docs/research/azure-devops-diff.md) compares, against
`git diff -U0` on the same SHAs. It also gathers the evidence of the four side questions of M7.01, and
writes the fixtures of M7.46. It reads Azure DevOps with plain REST calls and needs no package.

| Path | Calls | Compared with git |
|---|---|---|
| A | `Diffs - Get` (`diffs/commits`, `diffCommonCommit=true`, `$top`/`$skip`) | file list only: the API has no lines |
| B | `FileDiffs` (preview, `lineDiffBlocks`), in batches | added and removed lines of each file |
| C | `Blobs - Get` of `originalObjectId` and `objectId`, diffed by the port of git's xdiff of `AzureDevOpsDiff.psm1` | added and removed lines, with and without the indent heuristic |

The base is the `commonCommit` of `Diffs - Get` (the merge base of the target and the head). The head
is `lastMergeSourceCommit`, never the merge commit. The reference is
`git diff -U0 -M <base> <head>` in a local clone, with default options.

## What the owner runs

### 1. A test repository whose content may be published

Fixtures are copied into this public repository, so the measured repository must hold nothing
private. Import this repository itself into the test organization: Repos, Import repository,
`https://github.com/gaetanthouvenin/code-coverage.git`. Choose a project and a repository name of at
least four characters that are not common words. The script refuses to write a file that contains
them.

### 2. At least ten real pull requests

`develop` is merged by squash, so each commit is one real pull request. In a clone of the imported
repository:

```powershell
git clone https://dev.azure.com/<org>/<project>/_git/<repo> spike-clone
cd spike-clone
$picks = git log --format=%H -n 10 origin/develop          # 10 real pull requests, one commit each
$n = 0
foreach ($c in $picks) { $n++; git push origin "$($c)~1:refs/heads/spike/base-$n" "$($c):refs/heads/spike/head-$n" }
# A pull request of more than 100 files (40 commits at once), renames included:
git push origin "origin/develop~40:refs/heads/spike/base-wide" "origin/develop:refs/heads/spike/head-wide"
# A target that moved on (3 commits), with a binary and a text edit on the head.
git switch -c spike/head-moved origin/develop~3
Copy-Item src/CodeCoverage.Web/wwwroot/fonts/jetbrains-mono/JetBrainsMono-Variable-latin.woff2 spike-copy.woff2
Add-Content README.md "`nSpike M7.01."
git add spike-copy.woff2 README.md; git commit -m "spike: binary and text edit"
git push origin spike/head-moved "origin/develop:refs/heads/spike/base-moved"
```

Then open one pull request per pair, `spike/head-*` into `spike/base-*`. Use the web UI, or the Azure
CLI if you already use it:

```powershell
foreach ($name in @(1..10) + @('wide', 'moved')) {
  az repos pr create --org https://dev.azure.com/<org> --project <project> --repository <repo> `
    --source-branch "spike/head-$name" --target-branch "spike/base-$name" --title "spike M7.01 $name"
}
```

Leave them active: the query of question 3 needs active pull requests.

### 3. A token in the environment, never in a file

A personal access token of a member of the test organization, with **Code (Read)**, **Graph (Read)**
and **User Profile (Read)**. Add **Code (Read & write)** only for the optional thread probe. Set it in
the session that runs the script:

```powershell
$env:AZURE_DEVOPS_TOKEN = Read-Host -MaskInput 'PAT'
```

For question 4 as M7.54 will do it, run the script a second time with the token of the Entra
application once it exists (M7.12): `-TokenKind Bearer`, with an access token for the resource
`499b84ac-1321-427f-aa17-267ca6975798`.

### 4. The run

```powershell
pwsh tools/azure-devops-diff/Measure-AzureDevOpsDiff.ps1 -Organization <org> -Project <project> -Repository <repo> `
  -ClonePath ../spike-clone -Count 12 -FixturePath tests/fixtures/azure-devops -FixturePulls <id of spike 1>,<id of wide> -Execute
```

- Without `-Execute`, it prints the calls it would make and reads nothing.
- `-Pulls 12,13,14` measures given pull requests; without it, the `-Count` most recent active ones.
- `-ProbeThreadPull <id>` creates two threads on that pull request, updates comment 1 of each, then
  deletes it. This is the only write, and it answers question 1 for threads the application creates.
- The report (`report.md`, `report.json`) goes to a new folder under the temporary folder, or to
  `-OutputPath`. It names no organization, project, repository, person, e-mail, SHA or file path:
  paths become short hashes.
- Fixtures: `diffs/commits.json` (and `commits-page-<n>.json`), `diffs/filediffs.json`,
  `diffs/iteration-changes.json`, `diffs/blob-base.json` and `diffs/blob-head.json` come from the
  first pull request of `-FixturePulls`; `patches/pr-<id>.diff` comes from each of them. Metadata is
  scrubbed (names, GUIDs, e-mails, identities). A file that still holds a forbidden value is not
  written, and the report says which kind of value it held. Review the `git diff` before committing
  them.

Send back `report.md` and `report.json`: M7.01 fills in the measured columns of the research note
from them.

## Offline self-test

```bash
pwsh tools/azure-devops-diff/Test-AzureDevOpsDiff.ps1
```

No network. It checks the reading of `git diff -U0`, the `lineDiffBlocks` conversion and the
scrubbing. It compares the local diff with `git diff --no-index -U0` on edge cases and on 200 random
edits of the files of this repository. It then runs the measurement end to end against
`MockAzureDevOps.ps1`, a local stand-in that answers from a throwaway git repository.
