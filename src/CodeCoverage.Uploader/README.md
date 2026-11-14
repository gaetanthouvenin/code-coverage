# coverage-upload

The uploader of [CodeCoverage](https://github.com/gaetanthouvenin/code-coverage), a self-hosted
coverage service compatible with Codecov. It finds the coverage reports of a build (Cobertura,
OpenCover, LCOV, JaCoCo, Clover), detects the CI context (GitHub Actions, Azure Pipelines) and uploads
them to a CodeCoverage instance.

```bash
dotnet tool install --global CodeCoverage.Uploader
coverage-upload --url https://coverage-api.example.com --flags unit
```

On GitHub Actions the upload authenticates with the OIDC token of the job (`id-token: write`);
elsewhere, give an upload token with `--token` or `COVERAGE_TOKEN` (an imported `CODECOV_TOKEN`
keeps working). Every option falls back on a `COVERAGE_*` variable (`--flags` on `COVERAGE_FLAGS`).

The tool needs .NET 10 or later. Agents without .NET use the standalone binaries of the GitHub
releases, through the composite action or the Azure Pipelines template of the repository. A failed
upload never fails the build, unless `--fail-on-error`.

Licence: Apache-2.0.
