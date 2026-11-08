# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENT.md

## Claude Code: model and token savings

The repository does not force a `model`: everyone keeps the model of their own seat. Generated or vendored files (`bin/`, `obj/`, `wwwroot/lib/`) are not worth reading.

### Guidelines

- Read the micro-feature of `docs/PLAN.md` before coding it, and keep the change to its scope: one micro-feature, one commit, one pull request of a few files.
- Do not infer the cause of a bug without evidence (distinguish proven / probable / unproven cause).
- Before proposing a commit: `dotnet build -c Release` without warning, then `dotnet test`, then a review of the `git diff`.
- Build and test with reduced output: `dotnet build CodeCoverage.slnx -c Release -nologo -v q -clp:ErrorsOnly` and `dotnet test CodeCoverage.slnx --nologo -v q`, or a single test project instead of the solution. `Infrastructure.Tests`, `Api.Tests` and `Web.Tests` use Testcontainers for the database tests: without Docker, those tests are skipped with an explicit reason, never failed.
- Give precise paths or class names in requests rather than letting Claude explore the repository.
- Use plan mode before a change that touches several projects.
- `/clear` between two micro-features; `/compact` with an instruction (what to keep) when a long session must continue.
