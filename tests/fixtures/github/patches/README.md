# Real GitHub patches

Each `{name}.diff` is the exact `patch` field the GitHub REST API returned for one file. `{name}.before`
and `{name}.after` are that file at the two commits (absent when the file did not exist). No file here
is hand-written or edited.

| Fixture | Shows | Repository (MIT) | File | Base | Head |
|---|---|---|---|---|---|
| `multiple-hunks` | 5 hunks | `coverlet-coverage/coverlet` | `.editorconfig` | `6427ae3c8749` | `04ba205b93e5` |
| `no-newline-at-eof` | `\ No newline at end of file` | `coverlet-coverage/coverlet` | `.gitignore` | `6427ae3c8749` | `04ba205b93e5` |
| `added-single-line` | `@@ -0,0 +1 @@` | `coverlet-coverage/coverlet` | `.github/ISSUE_TEMPLATE/config.yml` | `6427ae3c8749` | `04ba205b93e5` |
| `removed-file` | `@@ -1,23 +0,0 @@` | `coverlet-coverage/coverlet` | `DeterministicBuild.targets` | `6427ae3c8749` | `04ba205b93e5` |
| `renamed-with-changes` | renamed and modified | `coverlet-coverage/coverlet` | `src/coverlet.collector/build/coverlet.collector.targets` | `6427ae3c8749` | `04ba205b93e5` |
| `removed-lua-comments` | removed `-- comment`, written `--- comment` | `nvim-telescope/telescope.nvim` | `lua/telescope/init.lua` | `f7c673b8e46e` | `d49d0567a730` |
