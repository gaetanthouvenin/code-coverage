# CodeCoverage — Agent Guide

> Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before writing a line of code and
> [docs/CONVENTIONS.md](docs/CONVENTIONS.md) before shaping it. This file lists the rules;
> the architecture explains them, the conventions give their exact form, and
> [docs/PLAN.md](docs/PLAN.md) cuts the work into micro-features.

---

## Critical rules (read first)

1. **Codecov formula and behaviour.** `coverage = hits / (hits + misses + partials)`. A repository
   that works with Codecov keeps the same `codecov.yml`, the same status names and the same
   secrets; every difference with Codecov is named, documented and configurable, except the
   differences of multi-organization hosting and of the forges that ADR-013 and ADR-014 name as not
   configurable. The compatibility holds for GitHub, Bitbucket and GitLab; Azure DevOps has no Codecov
   reference, so it follows GitHub and ADR-014 names its differences.
2. **The diff always comes from the forge API**, never from the git history of the CI agent
   (shallow clones must work). The commit is always the head of the pull request, never the
   synthetic merge commit.
3. **No heavy work in an HTTP request.** Uploads and webhooks are stored, queued in the database
   job queue and processed by the Api worker. Only the Api runs the worker and the migrations.
4. **Both database providers, always.** One EF Core 10 model for PostgreSQL and SQL Server; a model
   change comes with two migrations, one in `Infrastructure.PostgreSql`, one in
   `Infrastructure.SqlServer`, in the same commit. No provider-specific SQL outside the job queue
   dialects.
5. **The Api and the Web never handle a Domain type**, except in `Mapping`, `Program` and
   `Configuration`. The rule is checked on the IL, method bodies included: a query returning a
   Domain object is sent through a mapper extension on `ISender` (`Get{Thing}ResponseAsync`,
   `Get{Thing}ViewModelAsync`).
6. **Coverage tooling never breaks a build.** The uploader exits with 0 unless `--fail-on-error`.
7. **Settings live in the database**, are edited in the web console and reloaded without a restart
   (`IOptionsMonitor<T>`). The 4 settings of an organization (`SettingScope.Organization`) are read
   through the scoped `IOrganizationSettingsProvider` instead. The environment only carries the
   bootstrap values (connection string, provider, Data Protection); no functional parameter in
   `appsettings.json`.
8. **Isolation between organizations (ADR-013).** Every table of the model is classified in
   `TableClassification` (organization, operational or global); a test fails until a new one is.
   An organization table is declared with `IsOrganizationOwned`. `IgnoreQueryFilters` is never used
   in `src`, with no exception: cross-organization work is a coordinator job that queues one child
   per organization. A DI scope enters an organization only through the door of ADR-013:
   `AuthorizationBehavior` through `IOrganizationScope` for a message, `JobWorker` from the `Jobs`
   row for a job, and `SystemCommandRunner` for the administration command line once it exists
   (M1.22a). The build restricts `IOrganizationScope` to `AuthorizationBehavior`; no build rule
   covers the callers of `OrganizationContext.Enter`, which is only `internal` to the Infrastructure
   (ADR-013, residual risks).

---

## 1. Names

| Concern | Value |
|---|---|
| Product | CodeCoverage (in code: `CodeCoverage`) |
| Repository | `code-coverage` |
| Solution | `CodeCoverage.slnx` |
| Projects | `CodeCoverage.{Domain,Application,Infrastructure,Infrastructure.PostgreSql,Infrastructure.SqlServer,Api,Web,Uploader}` |
| Configuration section | `CodeCoverage:` (bootstrap) plus the setting groups of the database (`GitHub:`, `Uploads:`, `Processing:`…) |
| Organization table | shadow column `OrganizationId` and query filter `Organization`, declared by `IsOrganizationOwned` |
| Organization setting | `SettingScope.Organization` in `SettingDefinitions`, stored in `OrganizationSettings` |
| Hosts | two host names chosen at deployment, e.g. Web `coverage.example.com`, Api `coverage-api.example.com` |
| Images | `${IMAGE_PREFIX}-api`, `${IMAGE_PREFIX}-web`: `code-coverage-*` locally, `ghcr.io/<owner>/code-coverage-*` from the CI |
| Uploader | `dotnet tool` `coverage-upload` (`net10.0`), plus standalone binaries served by the Api |
| CSS prefix | `cc-` |

Everything that is code is in English: classes, tables, columns, error codes, settings, commit
messages.

## 2. Code conventions (docs/CONVENTIONS.md)

- **Copyright header** on every `.cs` and `.razor` file (`@* … *@` in Razor), followed by one
  blank line (the ReSharper settings `CodeCoverage.sln.DotSettings` apply it with
  « CodeCoverage: Full Cleanup »):

  ```csharp
  // ------------------------------------------------------------------------------------------------
  // <copyright file="FileName.cs" company="Gaëtan Thouvenin">
  //   Copyright (c) Gaëtan Thouvenin. All rights reserved.
  // </copyright>
  // ------------------------------------------------------------------------------------------------
  ```

- **MSDN-style XML documentation** on every `public` and `internal` member (none in tests).
- **`#region`** blocks in the order `Fields`, `Ctors`, `Properties`, `Methods`,
  `Implementation of …` (`Fields`, `Parameters`, `Injected services`, `Properties`, `Lifecycle`,
  `Methods` in a `.razor.cs`). The constructors region is `Ctors`, never `Constructors`; omit the
  regions without members.
- **One type per file**, no nested type; file-scoped namespace = project name + folder path; no
  folder named after a type.
- `TreatWarningsAsErrors` with the .NET analyzers and Roslynator (`src/default.ruleset`).
- `ConfigureAwait(false)` everywhere, `ConfigureAwait(true)` in `.razor.cs`;
  `ArgumentNullException.ThrowIfNull` at the start of every public method.
- **Razor**: markup and directives in `.razor`, all C# in the `.razor.cs` partial class — never an
  `@code` block. No hard-coded interface string: `IStringLocalizer<Common>`. The UI follows
  `docs/design/ui-mockup.html` (in-house components in `Web/Components/Ui`, no external UI component package).
- **Mediator**: martinothamar `Mediator` 3.x (source generator), **not MediatR**; pipeline
  `LoggingBehavior`, `AuthorizationBehavior`, `ValidationBehavior`; FluentValidation 12; EF Core 10.
- **`ApplicationException` alias.** The base of the HTTP-like refusals,
  `CodeCoverage.Application.Exceptions.ApplicationException`, conflicts with
  `System.ApplicationException` (implicit usings). Everywhere outside its own namespace, add:

  ```csharp
  using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;
  ```

  The HTTP status code is never carried by the exception: it is written only in the XML
  documentation (« Maps to HTTP 404 Not Found. ») and deduced from the type by the Api middleware
  and the Web error service.
- **Mappers** are explicit extension methods (`ToCommand`, `ToQuery`, `ToResponse`,
  `ToViewModel`); no mapping library, no `*Dto` type.

## 3. Where things go

| I need… | It goes in |
|---|---|
| A business rule, an entity, a value object, a read model | `Domain/{Feature}/` (namespace `CodeCoverage.Domain.{Feature}`); pure and synchronous, no package |
| Something from outside (database, GitHub, clock, user, secrets) | a port in `Domain/Ports/I{Thing}.cs`, implemented in `Infrastructure` (or in `{Host}/Configuration` when it depends on the request); its data types in `Domain/Contracts/{Area}/` |
| A use case | `Application/Features/{Feature}/Commands/{UseCase}/{UseCase}Command.cs` (`public sealed record`) + `…CommandHandler.cs` (`internal sealed`) + `…CommandValidator.cs`, or `…/Queries/{UseCase}/…Query.cs`. Handlers return Domain objects, `IReadOnlyList<T>`, `Unit` or an identifier: no DTO, no `Result<T>` |
| A refusal | an HTTP-like exception of `Application/Exceptions` (`BadRequestException` 400, `UnauthorizedException` 401, `ForbiddenException` 403, `NotFoundException` 404, `ConflictException` 409, `UnprocessableEntityException` 422, `BadGatewayException` 502) with a `Feature.Reason` code of `{Feature}Errors.cs`, translated in `Web/Resources/Common.resx` and `Common.fr.resx` |
| Reading or writing the database | a repository port (Domain objects in and out, never `IQueryable`, a mandatory `CancellationToken`, no unit of work), implemented in `Infrastructure/Persistence/Repositories/`, configured in `Persistence/Configurations/`, registered in `Infrastructure/DependencyInjection.Repositories.cs` |
| Background work | a job: constant in `Domain/Contracts/Jobs/JobTypes.cs`, case in `Infrastructure/Jobs/JobDispatcher.cs`, queued by the repository method of the change, in its transaction |
| An API endpoint | an MVC controller `Api/Controllers/{Resource}Controller.cs` under `/api/v1` (no minimal API), contracts in `Api/Contracts/`, mapper in `Api/Mapping/{Feature}Mapper.cs`; its XML `<summary>` / `<remarks>` feed Swagger and Scalar |
| A screen | `Web/Components/Pages/{Name}.razor(.cs)`, ViewModels in `Web/ViewModels/{Feature}/`, mapper in `Web/Mapping/{Feature}Mapper.cs`, sent through `UiRequestRunner` |
| A setting | a `SettingDefinition` of `Domain/Settings/SettingDefinitions`, bound to an options class read through `IOptionsMonitor<T>` |
| A setting of an organization | a `SettingDefinition` of `SettingScope.Organization` (never secret) and a member of `OrganizationSettingsSnapshot`, read through `IOrganizationSettingsProvider` |
| A table of an organization | `IsOrganizationOwned()` in its `Persistence/Configurations/` file (`HasOrganizationForeignKey()` only when its unique index starts with `OrganizationId`), its name in `TableClassification.OrganizationOwned`, and a test class on two organizations for its repository |

## 4. Tests

- xUnit v3, Shouldly, FakeItEasy (`tests/Directory.Build.props`); one test project per layer,
  plus `Uploader.Tests` and `Architecture.Tests`. Test names: `{Method}_{Scenario}_Should{Behavior}`.
- `Infrastructure.Tests` run every database test on **PostgreSQL and SQL Server** (Testcontainers).
  Without Docker, these tests are skipped with an explicit reason, never failed.
- `Api.Tests` and `Web.Tests` start the hosts with `WebApplicationFactory`; `Web.Tests` render the
  components with bUnit.
- Report parsers and totals are checked against the fixtures of `tests/fixtures/`; PR comments with
  Verify snapshots (`*.verified.txt`, LF, see `.gitattributes`).
- `Architecture.Tests` (NetArchTest and source checks) guard the layers, the feature layout, the
  ports, the headers and the regions: keep them green, never loosen them to make a change pass.

## 5. Checklist before a commit

- [ ] On a `feature/*` branch started from `develop` (GitFlow, `docs/CONVENTIONS.md` §15.1): never a
      commit on `main`, which only receives releases and hotfixes and publishes the images
- [ ] One micro-feature of `docs/PLAN.md`, nothing from a later one, with its GitHub issue (the
      `Issue` line under its title): branch `feature/{id}-{topic}`, pull request into `develop`
      with `Closes #{issue}`
- [ ] `dotnet build -c Release` without warning, `dotnet test` green
- [ ] Headers, XML docs, regions (`Ctors`), one type per file
- [ ] New error code: `{Feature}Errors.cs`, `Common.resx` and `Common.fr.resx`
- [ ] Model change: two migrations, the "no model change without migration" test green
- [ ] Commit message in English, conventional commit + gitmoji, e.g.
      `feat(app): ✨ Add the HTTP-like exception hierarchy`
