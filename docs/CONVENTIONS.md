# Conventions de code

> Document de référence pour la **forme** du code de CodeCoverage (remplaçant auto-hébergé de
> Codecov, .NET 10). `AGENT.md` liste les règles, `docs/ARCHITECTURE.md` les explique, ce document
> en donne la forme exacte, et `docs/PLAN.md` découpe le travail en micro-fonctionnalités. Les
> décisions d'architecture sont actées dans `docs/adr/`.

**Ordre de priorité en cas de désaccord** : 1) exigence explicite de l'utilisateur (signalée
« Exigence utilisateur » dans ce document) ; 2) ADR acceptée ; 3) ce document ; 4) le code existant
du dépôt.

**Règle générale** : tout ce qui est du code est en **anglais** (types, membres, tables, colonnes,
codes d'erreur, paramètres, messages de commit, documentation XML). Seuls les libellés affichés
sont traduits (`Web/Resources/Common.resx` et `Common.fr.resx`).

Les règles vérifiables sont gardées par `tests/CodeCoverage.Architecture.Tests` (voir §13.4) ; les
autres sont tenues par la revue et par le profil de nettoyage ReSharper (§3.7).

---

## Sommaire

1. Solution et projets
2. Fichiers racine, `Directory.Build.props`, ruleset, `.csproj`, packages
3. Style de code (en-tête, `using`, régions, nommage, types, documentation XML, analyseurs)
4. Couche Domain
5. Couche Application (CQRS, Mediator, behaviors)
6. Exceptions « proches HTTP »
7. Mappers entre couches
8. Infrastructure et persistance (deux fournisseurs)
9. Jobs en arrière-plan
10. Projet Api (contrôleurs, erreurs, OpenAPI Swagger + Scalar)
11. Projet Web (Blazor Server, `UiRequestRunner`, localisation, UI maison)
12. Paramètres rechargeables à chaud
13. Tests
14. Docker et CI
15. Commits et checklist
16. Synthèse des choix, écarts assumés et lacunes

---

## 1. Solution et projets

### 1.1 Projets

Projets nommés `CodeCoverage.<Layer>`, sans préfixe d'entreprise ; dossiers `src/` et `tests/` ;
un projet de tests par couche, plus `Uploader.Tests` et `Architecture.Tests`.

Exigence utilisateur : l'API et les vues sont dans **deux projets** distincts (ADR-002).

```text
CodeCoverage.slnx
CodeCoverage.sln.DotSettings
src/
  Directory.Build.props
  default.ruleset
  CodeCoverage.Domain/
  CodeCoverage.Application/
  CodeCoverage.Infrastructure/
  CodeCoverage.Infrastructure.PostgreSql/    (migrations et dialecte SQL de la file de jobs)
  CodeCoverage.Infrastructure.SqlServer/     (migrations et dialecte SQL de la file de jobs)
  CodeCoverage.Api/                          (REST /api/v1, OpenAPI, Swagger, Scalar, worker)
  CodeCoverage.Web/                          (Blazor Server)
  CodeCoverage.Uploader/                     (dotnet tool coverage-upload, autonome)
tests/
  Directory.Build.props
  CodeCoverage.Domain.Tests/
  CodeCoverage.Application.Tests/
  CodeCoverage.Infrastructure.Tests/
  CodeCoverage.Api.Tests/
  CodeCoverage.Web.Tests/
  CodeCoverage.Uploader.Tests/
  CodeCoverage.Architecture.Tests/
```

### 1.2 Dépendances autorisées

Vérifiées par `tests/CodeCoverage.Architecture.Tests/LayerDependencyTests.cs` sur le `.csproj`, sur
les références compilées et sur les types (NetArchTest) :

| Projet | Référence |
|---|---|
| Domain | rien : uniquement la BCL, ni projet ni package |
| Application | Domain seulement, aucune technologie (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Npgsql`, `Microsoft.Data.SqlClient`) |
| Infrastructure | Application, Domain |
| Infrastructure.PostgreSql / .SqlServer | Infrastructure |
| Api | Application ; Infrastructure pour la composition uniquement |
| Web | Application ; Infrastructure pour la composition uniquement (envoi en processus via `ISender`, jamais d'appel HTTP à l'Api : ADR-002) |
| Uploader | aucun projet de la solution : son seul couplage avec le serveur est le contrat HTTP |

Règle : **l'Api et les écrans partagent les mêmes commandes**. Tout ce qu'un écran fait est aussi
disponible via `/api/v1`, avec les mêmes validations et les mêmes permissions.

### 1.3 Solution `.slnx`

`CodeCoverage.slnx` (format XML). Dossiers de solution : `/config/` (fichiers racine et documents),
`/deploy/` (workflow, Dockerfiles, compose), `/src/` (`default.ruleset`, `Directory.Build.props` et
les projets), `/tests/` (`Directory.Build.props` et les projets de tests). Le fichier lui-même fait
foi.

Le fichier ReSharper s'appelle `<solution>.sln.DotSettings` même à côté d'un `.slnx` :
`CodeCoverage.sln.DotSettings` (§3.7).

### 1.4 Documents à la racine

`AGENT.md` (règles pour les agents et les développeurs), `CLAUDE.md` (renvoie à `AGENT.md`),
`README.md` ; dans `docs/` : `ARCHITECTURE.md`, `CONVENTIONS.md` (ce document), `PLAN.md`, `adr/`,
`design/` (`ui-mockup.html`).

---

## 2. Fichiers racine, `Directory.Build.props`, ruleset, `.csproj`, packages

Les fichiers racine appartiennent à ce dépôt et évoluent ici par PR normale. Aucun flux NuGet
privé : la restauration utilise nuget.org seul, en local, en CI et dans les Dockerfiles. Aucun
`nuget.config` n'est versionné (`/nuget.config` est ignoré, réservé à un flux local).

### 2.1 `.editorconfig`

Le fichier `.editorconfig` du dépôt fait foi (le lire plutôt que de le recopier). Points saillants :

- `indent_size = 2` (C# compris : `csharp_indent_size = 2`), `max_line_length = 100`,
  `end_of_line = crlf` (`lf` pour `*.sh`), `charset = utf-8`, `insert_final_newline = true` ;
- accolades toujours sur la ligne suivante (`next_line`) et toujours requises
  (`csharp_braces_for_ifelse = required`…) ;
- `var` partout ; modificateurs `private`/`internal` explicites ;
- corps de méthode en bloc (`csharp_method_or_operator_body = block_body`) ;
- une ligne vide autour et à l'intérieur des `#region` ;
- groupes de `using` séparés par une ligne vide (`csharp_blank_lines_between_using_groups = 1`) ;
- pas d'espace après un attribut (`csharp_space_after_attributes = false` → `[FromQuery]string? x`).

Le fichier ne contient que des clés ReSharper/Rider (`csharp_*`), aucune règle Roslyn
`dotnet_diagnostic.*` ni `file_header_template` : l'en-tête de fichier vient du `.DotSettings`
(§3.7). Le glob `[*.{cs, csx, cake, vb}]` contient des espaces : il est conservé tel quel.

### 2.2 `GitVersion.yml`

Voir le fichier `GitVersion.yml` : `workflow: GitFlow/v1`, `mode: ContinuousDelivery`,
`tag-prefix: "v"`, versions d'assembly `MajorMinorPatch` / `MajorMinorPatchTag`, et une étiquette
par branche (`main` sans étiquette, `develop` `beta` en incrément mineur, `release[/-]` `rc`,
`hotfix[/-]` `hotfix`, `feature[/-]` `alpha`). La CI utilise GitVersion 6.x (§14.1).

### 2.3 `global.json`

SDK épinglé avec `"rollForward": "latestFeature"` (voir `global.json` pour la version courante).
Pas de section `"test"` : les tests utilisent le runner VSTest (`Microsoft.NET.Test.Sdk` +
`xunit.runner.visualstudio`).

### 2.4 `.gitignore`, `.gitattributes`, `.dockerignore`, `.env`

- `.gitignore` : le `VisualStudio.gitignore` de GitHub, plus les données d'exécution des hôtes
  (`src/CodeCoverage.{Api,Web}/data/` et `logs/`), `/nuget.config`, les secrets locaux (`.env`,
  `.env.*` sauf `!.env.example`, `/secrets/`), `!tests/fixtures/**` (les rapports de couverture de
  test sont des entrées, pas des résultats) et la configuration Claude Code (`.claude/*` sauf
  `settings.json` et `agents/`).
- `.gitattributes` : `*.verified.txt text eol=lf working-tree-encoding=UTF-8` (snapshots Verify,
  comparés octet pour octet) et `*.woff2 binary`.
- `.dockerignore` : exclut les sorties de build, les données locales, `.git/`, `.github/`, les
  réglages d'IDE et d'agent, les secrets (`.env.*` sauf `!.env.example`, `*.pfx`, `secrets/`),
  `docs/`, `tests/`, `*.md`, `scripts/` et les fichiers de déploiement. À garder aligné sur ce que
  les Dockerfiles copient.
- `.env.example` versionné, `.env` ignoré.

### 2.5 `src/Directory.Build.props`

Pas de `Directory.Build.props` à la racine : un fichier dans `src/`, un autre dans `tests/`, et
`tests/` n'importe **pas** celui de `src/` (donc ni `TreatWarningsAsErrors`, ni ruleset, ni
analyseurs, ni documentation XML dans les tests).

Le fichier `src/Directory.Build.props` fait foi. Il fixe :

- l'identité : `Copyright`, `Company`, `Authors` au nom du titulaire du projet ; aucune
  `RepositoryUrl` ni `PackageProjectUrl` ;
- `TargetFramework` `net10.0`, `LangVersion` `preview`, `ImplicitUsings` et `Nullable` activés,
  `NeutralLanguage` `en-US`, `Features` `IOperation;flow-analysis` ;
- `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true` ;
- `ContinuousIntegrationBuild` et `Deterministic` à `true` ;
- les versions injectées par la CI (`NuGetVersion`, `AssemblySemVer`, `MajorMinorPatch`,
  `InformationalVersion`, `1.0.0` par défaut) et
  `IncludeSourceRevisionInInformationalVersion=false` (GitVersion porte déjà le commit) ;
- `CodeAnalysisRuleSet` = `default.ruleset`, `NuGetAuditLevel` `moderate`,
  `NoWarn` `NU1107;CS1591;1591` ;
- les analyseurs `Microsoft.CodeAnalysis.NetAnalyzers` `10.0.*` et `Roslynator.Analyzers` `4.15.*`
  (`PrivateAssets all`).

Choix du projet :

- `NuGetAuditLevel` : `moderate` ;
- pas de `Microsoft.VisualStudio.Threading.Analyzers` ;
- pas de cible MSBuild pour imposer le découpage des dossiers : ce sont les tests d'architecture
  qui le font (§13.4) ;
- `NoWarn` contient `CS1591;1591` : l'absence de documentation XML **ne casse pas** le build. La
  règle « documentation XML obligatoire » est tenue par convention et revue (§3.6).

### 2.6 `tests/Directory.Build.props`

Le fichier `tests/Directory.Build.props` fait foi : `net10.0`, `IsPackable=false`,
`IsTestProject=true`, `RestorePackagesWithLockFile=false`, `NoWarn` `NU1507` ; packages `xunit.v3`
`3.2.*`, `xunit.runner.visualstudio` `3.1.*`, `Microsoft.NET.Test.Sdk` `18.*`, `Shouldly` `4.3.*`,
`FakeItEasy` `9.0.*` ; `using` globaux `FakeItEasy`, `Shouldly`, `Xunit` ; `TestResults\**` et
`coverage.opencover.xml` retirés des `None`.

Choix : pas de coverlet ni de seuil dans les props ; la collecte de couverture se décide en CI
(§14.2).

### 2.7 `src/default.ruleset`

Le fichier `src/default.ruleset` fait foi (`RuleSet Name="CodeCoverage Rules"`). Il désactive ou
abaisse en `Info` un petit nombre de règles jugées trop bruyantes : `AsyncFixer01`, `AV1755`,
`IDE0008`, `IDE0058`, `IDE0160`, `CA1030`, `CA1031`, `CA1835`, `RCS1174` désactivées ;
`CA1032`, `CA1040`, `CA1054`, `CA1056`, `CA1062`, `CA1303`, `CA1308`, `CA1710`, `CA1716`,
`CA1819`, `RCS1194` en `Info`. Ni `VSTHRD200` ni règles Sonar.

### 2.8 `.csproj` minimal

Aucun `TargetFramework` ni `Nullable` dans les `.csproj` (ils viennent des props) : seulement
`InternalsVisibleTo`, `ProjectReference`, `PackageReference`. Exemple :
`src/CodeCoverage.Application/CodeCoverage.Application.csproj` (avec
`InternalsVisibleTo Include="DynamicProxyGenAssembly2"`, nécessaire pour que FakeItEasy simule des
interfaces `internal`, et le générateur de source du médiateur en `PrivateAssets all`, §5.5).

Les hôtes publient en dossier, jamais en fichier unique (`PublishSingleFile` faux).

### 2.9 Packages et versions

**Pas de Central Package Management** : versions flottantes directement dans chaque `.csproj`,
majeure fixée (`10.*`, `12.*`, `3.*`…). Principaux packages :

| Usage | Paquet | Version | Projet |
|---|---|---|---|
| Médiateur | `Mediator.Abstractions`, `Mediator.SourceGenerator` (Application seule) | `3.*` | Application |
| Validation | `FluentValidation`, `FluentValidation.DependencyInjectionExtensions` | `12.*` | Application |
| Injection, logs | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | `10.*` | Application |
| Options | `Microsoft.Extensions.Options.ConfigurationExtensions` | `10.*` | Infrastructure |
| EF Core | `Microsoft.EntityFrameworkCore`, `.Relational`, `.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.*` | Infrastructure |
| EF Core outils | `Microsoft.EntityFrameworkCore.Design` | `10.*` (`PrivateAssets all`) | projets de migrations |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | `10.*` | Infrastructure |
| Data Protection | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Azure.Extensions.AspNetCore.DataProtection.Keys`, `Azure.Identity` | `10.*` / `1.*` | Infrastructure |
| OpenAPI | `Swashbuckle.AspNetCore` `10.*`, `Scalar.AspNetCore` `2.*` | — | Api |
| Logs | `Serilog.AspNetCore` `10.*`, `Serilog.Sinks.Console` `6.*`, `Serilog.Sinks.File` `7.*`, `Serilog.Formatting.Compact` `3.*`, `Serilog.Enrichers.Environment` `3.*`, `Serilog.Enrichers.Thread` `4.*` | — | Api, Web |
| Télémétrie | `OpenTelemetry.*` | `1.*` | Api, Web |
| Tests (props) | `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `FakeItEasy` | §2.6 | tous les tests |
| Tests architecture | `NetArchTest.Rules` `1.3.*`, `Microsoft.CodeAnalysis.CSharp` `5.0.*` | — | Architecture.Tests |
| Tests intégration | `Microsoft.AspNetCore.Mvc.Testing` `10.0.*`, `Testcontainers.PostgreSql` / `.MsSql` `4.*` | — | Infrastructure/Api/Web.Tests |
| Tests Blazor | `bunit` | `2.*` | Web.Tests |
| Snapshots | `Verify.XunitV3` | au besoin | commentaires de PR |
| Analyse (props) | `Microsoft.CodeAnalysis.NetAnalyzers` `10.0.*`, `Roslynator.Analyzers` `4.15.*` | — | `src/` |

**Ne pas utiliser** : `MediatR` (exigence utilisateur, ADR-001 : les versions 13 et suivantes sont
sous licence commerciale), aucune bibliothèque de mapping (AutoMapper, Mapster, Mapperly), aucune
bibliothèque de composants Blazor (MudBlazor, Fluent UI) : les composants sont maison (§11.6),
aucun paquet d'un flux privé.

---

## 3. Style de code

### 3.1 En-tête de fichier

Obligatoire sur **chaque** `.cs` et `.razor` de `src/` et `tests/`, tests compris, suivi d'**une
ligne vide**. Forme exacte (séparateur : `// ` + 96 tirets, 99 caractères ; seule variable : le nom
du fichier ; ni auteur ni date) :

```csharp
// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

```

En Razor, le même texte dans un commentaire `@* … *@` (voir
`src/CodeCoverage.Web/Components/Pages/Home.razor`) :

```razor
@* ----------------------------------------------------------------------------------------------
   <copyright file="Home.razor" company="Gaëtan Thouvenin">
     Copyright (c) Gaëtan Thouvenin. All rights reserved.
   </copyright>
   ---------------------------------------------------------------------------------------------- *@

```

L'en-tête est appliqué par le profil de nettoyage « CodeCoverage: Full Cleanup » (§3.7) et vérifié
ligne à ligne par `SourceFileConventionTests.SourceFiles_ShouldStartWithCopyrightHeader`.

### 3.2 `using` et namespace

- `using` **hors** du namespace, groupés par racine, `System.*` en premier, puis les packages,
  puis `CodeCoverage.*`, une ligne vide entre groupes ; les alias (`using X = …;`) en dernier.
- Namespace **file-scoped** ; namespace = nom du projet + chemin du dossier.
- Aucun dossier ne porte le nom d'un type (`Configuration`, pas `Settings` quand une classe
  `Settings` existe) ni `System` : le namespace masquerait le type.

Exemple (`src/CodeCoverage.Application/Behaviors/LoggingBehavior.cs`) :

```csharp
using System.Diagnostics;

using Mediator;

using Microsoft.Extensions.Logging;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Application.Behaviors;
```

### 3.3 Régions

`#region` dans chaque classe ; omettre les régions sans membre ; toujours fermer par
`#endregion`. **La région des constructeurs s'appelle `Ctors`, jamais `Constructors`**
(vérifié par `SourceFileConventionTests.SourceFiles_ShouldNeverUseConstructorsRegion`).

Ordre pour une classe C# :

1. `#region Fields` — champs **et constantes** (`public const int TypeMaxLength = 64;` est dans
   `Fields`) ;
2. `#region Ctors` ;
3. `#region Properties` ;
4. `#region Methods` — y compris fabriques statiques et méthodes privées ;
5. `#region Implementation of IXxx<A,B>` — nom généré par ReSharper, arguments génériques
   **sans espace** après la virgule (`Implementation of IPipelineBehavior<TMessage,TResponse>`) ;
6. (`#region Operators`, `#region Events` si nécessaire.)

Ordre pour un `.razor.cs` : `Fields` → `Parameters` → `Injected services` → `Properties` →
`Lifecycle` → `Methods` (puis `Implementation of …`). Exemple :
`src/CodeCoverage.Web/Components/Layout/MainLayout.razor.cs`.

Mise en forme (`.editorconfig`) : une ligne vide avant et après chaque région, une ligne vide à
l'intérieur après `#region` et avant `#endregion`, région indentée avec le corps de la classe.

Avec un constructeur primaire, il n'y a souvent pas de région `Ctors` (les handlers n'ont que
`Implementation of …`). La région `Ctors` sert aux entités (constructeur privé), aux value objects,
aux validateurs (le constructeur contient les `RuleFor`) et aux exceptions.

### 3.4 Nommage

| Élément | Règle |
|---|---|
| Champ privé | `_camelCase` |
| `const`, `static readonly` | `PascalCase` (`TypeMaxLength`, `MessageName`) |
| Méthode async | suffixe `Async`, actions de contrôleur comprises (`PingAsync`) ; exception : `Handle` des handlers et behaviors, imposé par Mediator (§5.5) |
| Paramètre de constructeur primaire | `camelCase`, utilisé directement, sans champ (`sender`, `store`) |
| Interface | préfixe `I` |
| Commande / requête | `{Verb}{Noun}Command`, `Get…Query` / `Search…Query` |
| Handler / validateur | `{UseCase}CommandHandler`, `{UseCase}CommandValidator` (`…QueryHandler`) |
| Codes d'erreur | `{Feature}Errors` (classe), `Feature.Reason` (valeur) |
| Contrats Api | `{Verb}{Thing}Request`, `{Thing}Response` (§10.2) |
| ViewModels | `{Thing}ViewModel` dans `Web/ViewModels/{Feature}/` |
| Composants UI maison | préfixe `Cc` (`CcButton`), classes CSS `cc-` |
| Jamais | suffixe `Dto`, dossier `Dto`/`Dtos` (vérifié par `FeatureLayoutTests.Types_ShouldNotBeSuffixedDto`) |

### 3.5 Types et fonctionnalités du langage

- `sealed` partout (`public sealed class`, `internal sealed class`), sauf classes abstraites,
  statiques et partielles Razor.
- Handlers, validateurs, behaviors, configurations EF, repositories, mappers : `internal`.
  Les handlers sont `internal sealed` (vérifié par `HandlerConventionTests`).
- Commandes, requêtes, contrats d'API : `public sealed record` positionnel.
- Entités : `sealed class`, propriétés `{ get; private set; }` (ou `{ get; }`), constructeur
  privé, fabrique statique (`Create`, `TryCreate`, `Parse`) qui vérifie les invariants.
- Constructeurs primaires pour l'injection (handlers, behaviors, contrôleurs, services).
- `ArgumentNullException.ThrowIfNull(...)` en tête de chaque méthode publique
  (`ArgumentException.ThrowIfNullOrWhiteSpace` pour une chaîne).
- `ConfigureAwait(false)` partout, sauf dans les `.razor.cs` et `UiRequestRunner`
  (`ConfigureAwait(true)`, le contexte du circuit Blazor est nécessaire).
- `var _ =>` comme cas par défaut des `switch` d'expression.
- **Un type par fichier, pas de type imbriqué**, ni plusieurs types de premier niveau dans un même
  `.cs`, en production comme dans les tests (vérifié par
  `SourceFileConventionTests.SourceFiles_ShouldDeclareOneTopLevelType` ; un bloc `extension` n'est
  pas un type).
- Pas de bloc `@code` dans les `.razor` : tout le C# va dans le `.razor.cs` (les analyseurs ne
  voient pas le code généré par Razor).
- Aucune chaîne d'interface en dur : `IStringLocalizer<Common>` (§11.5).

### 3.6 Documentation XML (style MSDN)

Style : résumé concis d'abord, puis `param`, `returns`, `remarks` et `exception` quand c'est utile.
`<example>` n'est pas utilisé.

Règles :

- en anglais ; sur **tout** membre `public` et `internal` (types, méthodes, propriétés,
  constantes) ; **jamais** sur les classes de tests (un commentaire `//` en tête de classe suffit) ;
- `<summary>` sur une ligne si court ; sinon texte indenté de 2 espaces après `/// ` ;
- propriétés : « Gets … » ; booléens : « Gets a value indicating whether … » ;
- constructeurs : « Initializes a new instance of the <see cref="X" /> class. » ;
- implémentations et overrides : `/// <inheritdoc />` (espace avant `/>`, de même pour
  `<see cref="X" />` et `<see langword="null" />`) ;
- records et constructeurs primaires : `<param name="…">` sur le type ;
- `<exception cref="…">` pour chaque exception levée volontairement, y compris
  `ArgumentNullException` ;
- exceptions HTTP-like : le statut HTTP est écrit dans le résumé (« Maps to HTTP 404 Not Found. »,
  §6) ;
- contrôleurs : `<summary>` = titre court, `<remarks>` = description (deviennent le résumé et la
  description OpenAPI, §10.5).

Exemples complets dans le dépôt : `src/CodeCoverage.Domain/Contracts/Jobs/JobRequest.cs` (entité
avec constantes, constructeur privé, fabrique et exceptions documentées),
`src/CodeCoverage.Domain/Ports/ISettingsStore.cs` (port), `src/CodeCoverage.Web/Messaging/UiResult.cs`
(booléens « Gets a value indicating whether »).

### 3.7 Analyseurs et nettoyage ReSharper

- `TreatWarningsAsErrors` avec les analyseurs .NET et Roslynator, réglés par `src/default.ruleset`
  (§2.5, §2.7). `dotnet build -c Release` doit passer sans avertissement.
- `CodeCoverage.sln.DotSettings` (le fichier du dépôt fait foi) fournit l'en-tête de fichier
  (`FileHeaderText`, §3.1) et le profil de nettoyage **« CodeCoverage: Full Cleanup »**
  (`CSUpdateFileHeader`, `CSReorderTypeMembers`, `CSOptimizeUsings`, `CSReformatCode`,
  `CSharpFormatDocComments`, `CSMakeFieldReadonly`…). Le lancer avant chaque commit
  (ReSharper/Rider : Code Cleanup → « CodeCoverage: Full Cleanup »).
- Aucun File Layout ReSharper ne crée les régions : elles sont écrites à la main ; seuls le nom
  `Ctors` et l'en-tête sont vérifiés par les tests (§16.3).

---

## 4. Couche Domain

Exigence utilisateur : « Domain : Ports, Contracts, et objets métiers ». Le Domain est pur et
synchrone, sans aucun package (§1.2).

### 4.1 Arborescence

Rangement par feature (même découpage que l'Application et l'Infrastructure, namespaces =
dossiers), avec `Ports/` et `Contracts/` séparés :

```text
CodeCoverage.Domain/
  Ports/                 I{Thing}.cs : IClock, ICurrentUser, ISettingsStore, IJobQueue, IAuditLog…
  Contracts/{Area}/      types de données des ports (Contracts/Jobs/JobRequest.cs, JobTypes.cs)
  Operations/            entités, value objects, read models de la feature « Operations »
  Settings/
  Users/
  IDomainEvent.cs        seuls les types partagés par toutes les features à la racine
  IHasDomainEvents.cs
```

### 4.2 Ports

Un port est une interface de `Domain/Ports/I{Thing}.cs`, implémentée dans `Infrastructure` (ou
dans `{Host}/Configuration` quand elle dépend de la requête). Règles (vérifiées par
`tests/CodeCoverage.Architecture.Tests/PortConventionTests.cs`) :

- `CancellationToken` obligatoire sur toute méthode asynchrone, **sans valeur par défaut** ;
- retour : types du Domain ou `IReadOnlyList<T>`, **jamais** `IQueryable` ni DTO ;
- pas d'unité de travail exposée (ni `SaveChanges`, `Commit`, `BeginTransaction`…, ni type
  `UnitOfWork`, `DbContext`, `Transaction`) : le repository enregistre immédiatement ; un
  changement sur plusieurs agrégats = une méthode de repository (une transaction) ;
- un handler n'enregistre jamais rien lui-même.

Exemples : `src/CodeCoverage.Domain/Ports/IClock.cs` (le plus court) et
`src/CodeCoverage.Domain/Ports/ISettingsStore.cs` (lecture, liste, enregistrement « saved at
once »). Forme générique d'un repository :

```csharp
/// <summary>The repositories whose coverage is tracked.</summary>
public interface IRepositoryRepository
{
  #region Methods

  /// <summary>Finds a repository.</summary>
  /// <param name="id">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The repository, or <see langword="null" />.</returns>
  Task<TrackedRepository?> FindAsync(long id, CancellationToken cancellationToken);

  /// <summary>Adds a repository, saved at once.</summary>
  /// <param name="repository">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when the repository is saved.</returns>
  Task AddAsync(TrackedRepository repository, CancellationToken cancellationToken);

  #endregion
}
```

### 4.3 Entités et read models

`sealed class`, constructeur privé dans `#region Ctors`, `{ get; private set; }`, collections
exposées en `IReadOnlyList<T>` sur un `private readonly List<T> _items = [];`, fabrique statique,
invariants vérifiés dans la fabrique (exemple : `JobRequest.Create`). Les read models construits
pour les écrans et l'API sont aussi des types du Domain. EF Core mappe les entités du Domain
directement (pas de modèle de persistance séparé).

### 4.4 Événements de domaine

`IDomainEvent` (interface marqueur) et `IHasDomainEvents` (`DomainEvents`, `ClearDomainEvents()`)
existent dans le Domain pour l'avenir. Le MVP n'a pas d'événements de domaine : les enchaînements
passent par la file de jobs (§9, ADR-001, ADR-005).

---

## 5. Couche Application (CQRS, Mediator, behaviors)

### 5.1 Arborescence

Exigence utilisateur : « Séparations par features/Commands|Queries/UseCases ».

```text
CodeCoverage.Application/
  Behaviors/
    LoggingBehavior.cs
    AuthorizationBehavior.cs
    ValidationBehavior.cs
  Exceptions/
    ApplicationException.cs, BadRequestException.cs, ValidationException.cs, …
    ErrorCodes/GeneralErrors.cs
  Security/                          IAdminOnly, ISystemOnly, IRepositoryScoped, SecurityErrors
  Features/
    Settings/
      Commands/
        UpdateSetting/
          UpdateSettingCommand.cs
          UpdateSettingCommandHandler.cs
          UpdateSettingCommandValidator.cs
      Queries/
        GetSettings/
          GetSettingsQuery.cs
          GetSettingsQueryHandler.cs
      SettingErrors.cs               (codes d'erreur de la feature)
      SettingRules.cs                (règles partagées par les cas d'usage de la feature)
  DependencyInjection.cs
```

Règles (vérifiées par `tests/CodeCoverage.Architecture.Tests/FeatureLayoutTests.cs`) :

- chaque cas d'usage a son dossier `Features/{Feature}/{Commands|Queries}/{UseCase}/` ;
- commande, handler et validateur sont des fichiers séparés, nommés d'après le dossier
  (`{UseCase}Command`, `{UseCase}CommandHandler`, `{UseCase}CommandValidator`) ; une commande vit
  sous `Commands`, une requête sous `Queries` ;
- jamais de dossier `Dtos` ni de type `*Dto` ;
- validateur seulement quand il y a quelque chose à valider.

### 5.2 Retours

Un handler renvoie un objet du Domain, une liste `IReadOnlyList<T>` du Domain, un identifiant ou
rien (`Unit`). Les refus passent par des exceptions (§6). **Pas de DTO, pas de `Result<T>`.** La
traduction entre couches est faite par des mappers dans la couche consommatrice (§7).

### 5.3 Gabarits

Exemples de référence dans le dépôt :

- commande : `Features/Settings/Commands/UpdateSetting/UpdateSettingCommand.cs`
  (`public sealed record UpdateSettingCommand(string Key, string Value) : ICommand, IAdminOnly;`) ;
- handler : `UpdateSettingCommandHandler.cs` (`internal sealed`, constructeur primaire,
  `#region Implementation of ICommandHandler<UpdateSettingCommand,Unit>`, `<inheritdoc />` plus les
  `<exception>` levées, `ArgumentNullException.ThrowIfNull(command)`, `ConfigureAwait(false)`) ;
- validateur : `UpdateSettingCommandValidator.cs` (`internal sealed`, `AbstractValidator<T>`,
  règles dans le constructeur sous `#region Ctors`, code d'erreur posé par
  `ValidationFailure.ErrorCode`) ;
- requête : `Features/Settings/Queries/GetSettings/GetSettingsQuery.cs`
  (`public sealed record GetSettingsQuery : IQuery<IReadOnlyList<SettingValue>>, IAdminOnly;`).

Squelette d'un handler :

```csharp
/// <summary>Handler for the <see cref="CreateRepositoryCommand" /> command.</summary>
/// <param name="repositories">The tracked repositories.</param>
/// <param name="clock">The clock.</param>
internal sealed class CreateRepositoryCommandHandler(IRepositoryRepository repositories, IClock clock)
  : ICommandHandler<CreateRepositoryCommand, long>
{
  #region Implementation of ICommandHandler<CreateRepositoryCommand,long>

  /// <inheritdoc />
  public async ValueTask<long> Handle(CreateRepositoryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var repository = TrackedRepository.Create(command.Name, command.DefaultBranch, clock.UtcNow);

    await repositories.AddAsync(repository, cancellationToken).ConfigureAwait(false);
    return repository.Id;
  }

  #endregion
}
```

### 5.4 Codes d'erreur

Format **`Feature.Reason`** (PascalCase des deux côtés). Une classe `public static class
{Feature}Errors` à la racine de la feature, constantes dans `#region Fields`, chacune documentée.
Exemple : `src/CodeCoverage.Application/Features/Settings/SettingErrors.cs`
(`public const string Unknown = "Setting.Unknown";`). Les codes communs sont dans
`Exceptions/ErrorCodes/GeneralErrors.cs` (`General.ValidationFailed`, `General.Unexpected`), ceux
de la sécurité dans `Security/SecurityErrors.cs`.

Tout nouveau code est traduit dans `Web/Resources/Common.resx` **et** `Common.fr.resx`, sous son
propre nom (§11.5). Un code publié n'est jamais renommé.

### 5.5 Médiateur : martinothamar `Mediator` 3.x

Exigence utilisateur : remplacer MediatR. Décision : **martinothamar `Mediator` 3.x** (licence
MIT, générateur de source), ADR-001.

- `Mediator.Abstractions` et `Mediator.SourceGenerator` (`3.*`) ; **le générateur est référencé
  uniquement par Application** (`PrivateAssets all`), vérifié par
  `LayerDependencyTests.MediatorSourceGenerator_ShouldOnlyBeReferencedByApplication` : un seul
  médiateur généré par artefact déployé, qui voit les handlers `internal sealed` sans
  `InternalsVisibleTo`.
- Messages : `ICommand` / `ICommand<T>` et `IQuery<T>` ; handlers : `ICommandHandler<,>` et
  `IQueryHandler<,>`.
- **Écart assumé** : la méthode s'appelle `Handle` et renvoie `ValueTask<T>` (nom et type imposés
  par la bibliothèque), au lieu du suffixe `Async` et de `Task<T>`. Les actions de contrôleur et
  toutes les autres méthodes asynchrones gardent le suffixe `Async`.
- La signature v3 de `IPipelineBehavior` (message, `next`, `cancellationToken`) est figée par
  `tests/CodeCoverage.Application.Tests/Messaging/MediatorSignatureTests.cs`.
- Appelants : `ISender` (Api, et worker avec un scope par job) ; `ScopedSender` enveloppé par
  `UiRequestRunner` (Web, un scope DI par requête, §11.3).

### 5.6 Pipeline et behaviors

Ordre (externe → interne) : **Logging → Authorization → Validation**. Tout refus est journalisé,
et un message que le principal n'a pas le droit d'envoyer est refusé avant que son contenu soit
validé (`PipelineOrderTests`).

- `LoggingBehavior` (`src/CodeCoverage.Application/Behaviors/LoggingBehavior.cs`) : requêtes en
  `Debug`, commandes en `Information` ; un refus (`ApplicationException`) est une réponse,
  journalisé en `Warning` avec son code d'erreur ; toute autre exception en `Error`. Seul le nom du
  type du message est journalisé, jamais son contenu (ni jeton ni flux d'upload dans les logs).
- `AuthorizationBehavior` : le principal système (jobs, webhooks) passe tout ; un message
  `ISystemOnly` envoyé par un autre est refusé (`ForbiddenException`) ; un message `IAdminOnly`
  exige un utilisateur connecté (`UnauthorizedException`) administrateur (`ForbiddenException`) ;
  un message sans marqueur passe (son handler l'authentifie au besoin, par exemple un jeton
  d'upload). Les marqueurs sont des interfaces vides de `Application/Security`.
- `ValidationBehavior` : exécute **séquentiellement** chaque validateur (ils sont scoped, comme le
  `DbContext` qu'une règle asynchrone peut interroger), avec un `ValidationContext` par validateur,
  et lève la `ValidationException` **du projet** (§6.4), jamais celle de FluentValidation
  (qualifier le `using`).
- L'audit des opérations sensibles n'est pas un behavior : le handler appelle explicitement le port
  `IAuditLog` (exemple : `UpdateSettingCommandHandler`).

### 5.7 Enregistrement (`DependencyInjection.cs`)

Chaque couche expose `public static class DependencyInjection` avec une méthode
`Add{Layer}(this IServiceCollection services)` (`AddApplication`, `AddInfrastructure`). Voir
`src/CodeCoverage.Application/DependencyInjection.cs` :

- `services.AddMediator(...)` avec `Namespace = "CodeCoverage.Application.Messaging"`,
  `ServiceLifetime = ServiceLifetime.Scoped` (le `DbContext` est scoped),
  `GenerateTypesAsInternal = true`, `Assemblies = [typeof(DependencyInjection)]` et
  `PipelineBehaviors` dans l'ordre du §5.6 (les options sont lues par le générateur : constantes
  de compilation) ;
- `services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true)` ;
- un appel, et un seul, à la méthode de chaque zone.

Dans Application, la classe est `public static partial` : les services de `Application/Services`
s'enregistrent dans le fichier de leur zone, à côté de `DependencyInjection.cs`, pour que deux
micro-features de zones différentes ne modifient pas le même fichier.

| Zone | Fichier | Méthode `private static` |
|---|---|---|
| Commits | `DependencyInjection.Commits.cs` | `AddCommitServices` |
| Uploads | `DependencyInjection.Uploads.cs` | `AddUploadServices` |
| Comparaisons | `DependencyInjection.Comparisons.cs` | `AddComparisonServices` |
| Notifications | `DependencyInjection.Notifications.cs` | `AddNotificationServices` |
| Accès | `DependencyInjection.Access.cs` | `AddAccessServices` |

**Jamais d'enregistrement par balayage d'un namespace** : chaque service est enregistré une ligne à
la fois, avec sa durée de vie explicite. La durée de vie fait partie du comportement : un
`UploadProcessor` qui porte un `SemaphoreSlim` limite le processus entier, et une durée de vie
scoped donnerait une limite par job. Les options de `AddMediator` restent dans `AddApplication`.

---

## 6. Exceptions « proches HTTP »

### 6.1 Principes

- Une hiérarchie sous la base abstraite
  `CodeCoverage.Application.Exceptions.ApplicationException(errorCode, message)`, dans
  `Application/Exceptions/`.
- Le code HTTP n'est **pas** porté par l'exception (l'Application ne connaît pas HTTP) : il est
  écrit seulement dans la documentation XML (« Maps to HTTP 404 Not Found. ») et déduit du type par
  le middleware de l'Api (§10.3) et par le service d'erreurs du Web (§11.4).
- Chaque refus porte un code `Feature.Reason` (§5.4).

### 6.2 Liste

| Exception (`Application/Exceptions/`) | HTTP | Usage |
|---|---|---|
| `ApplicationException` (abstraite, `ErrorCode`) | 400 par défaut | Base de tout refus |
| `BadRequestException` | 400 | Requête incohérente hors règles de validation |
| `ValidationException` (`Errors` par propriété) | **400** | Échec des validateurs FluentValidation |
| `UnauthorizedException` | 401 | Aucun utilisateur / jeton invalide |
| `ForbiddenException` | 403 | Permission manquante |
| `NotFoundException` | 404 | Entité inconnue, ou cachée au principal |
| `ConflictException` | 409 | Doublon, état concurrent |
| `UnprocessableEntityException` | 422 | Règle métier refusée sur une requête valide |
| `BadGatewayException` | 502 | Service externe (GitHub…) indisponible |
| toute autre exception | 500 | Bug ; message jamais renvoyé au client |

Validation = 400, règle métier = 422.

`RetryLaterException(delay, message)` n'est **pas** une `ApplicationException` : levée par un
handler de job qui ne peut pas encore aboutir, elle demande au worker de relancer le job après un
délai sans consommer de tentative ; jamais exposée en HTTP, sans code d'erreur.

### 6.3 Base et sous-classes

Voir `src/CodeCoverage.Application/Exceptions/ApplicationException.cs` : deux constructeurs
`protected` (`errorCode, message` et `errorCode, message, innerException`) qui refusent un code
vide, propriété `ErrorCode`. Chaque sous-classe est `public sealed`, avec les deux constructeurs
publics correspondants sous `#region Ctors`, et le statut HTTP dans son résumé.

`NotFoundException` offre en plus une fabrique
(`src/CodeCoverage.Application/Exceptions/NotFoundException.cs`) :

```csharp
public static NotFoundException For(string entity, string id)
```

qui produit le code `{entity}.NotFound` et le message `{entity} {id} does not exist.`.

### 6.4 `ValidationException`

`src/CodeCoverage.Application/Exceptions/ValidationException.cs` : construite depuis
`IEnumerable<ValidationFailure>`, expose `IReadOnlyDictionary<string, string[]> Errors` groupé par
`PropertyName`, code `GeneralErrors.ValidationFailed` (`"General.ValidationFailed"`).

### 6.5 Alias obligatoire

Le nom `ApplicationException` entre en conflit avec `System.ApplicationException` (implicit
usings). Partout hors de son namespace, ajouter :

```csharp
using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;
```

---

## 7. Mappers entre couches

Exigence utilisateur : ne pas traiter d'objet du Domain au niveau des vues ni de l'API.

- Aucune bibliothèque de mapping : mappers manuels en méthodes d'extension,
  `internal static class {Feature}Mapper`, `#region Methods`.
- L'Application reçoit des commandes/requêtes et rend des objets du Domain.
- L'Api convertit `Request → Command/Query` et `Domain → Response` dans `Api/Mapping/` ;
  le Web convertit `Domain → ViewModel` et `ViewModel (formulaire) → Command` dans `Web/Mapping/`.
- Noms : `ToCommand`, `ToQuery`, `ToResponse`, `ToResponses`, `ToViewModel`, `ToViewModels`.
- **Une requête qui renvoie un objet du Domain est envoyée par une extension de mapper sur
  `ISender`** : `Get{Thing}ResponseAsync` (Api), `Get{Thing}ViewModelAsync` (Web). Le contrôleur ou
  la page ne nomme ainsi jamais un type du Domain, même dans le corps d'une méthode.
- `ArgumentNullException.ThrowIfNull` en tête ; listes en `IReadOnlyList<T>` via
  `[.. items.Select(ToResponse)]`.

La règle est vérifiée sur l'IL, corps de méthodes et arguments génériques compris, par
`PresentationIsolationTests.ApiAndWeb_ShouldNotReferenceDomain_ExceptMappingProgramAndConfiguration` :
hors des namespaces `{Host}.Mapping`, `{Host}.Configuration` et de `Program`, l'Api et le Web ne
référencent aucun type de `CodeCoverage.Domain`.

Forme générique (Api) :

```csharp
/// <summary>Converts the repositories between the Domain and the API.</summary>
internal static class RepositoryMapper
{
  #region Methods

  /// <summary>Builds the command that starts tracking a repository.</summary>
  /// <param name="request">The request.</param>
  /// <returns>The command.</returns>
  public static CreateRepositoryCommand ToCommand(this CreateRepositoryRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    return new CreateRepositoryCommand(request.Name, request.DefaultBranch);
  }

  /// <summary>Gets a repository and converts it for the API.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="id">The repository.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The response.</returns>
  public static async Task<RepositoryResponse> GetRepositoryResponseAsync(
    this ISender sender,
    long id,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    var repository = await sender.Send(new GetRepositoryQuery(id), cancellationToken).ConfigureAwait(false);
    return repository.ToResponse();
  }

  private static RepositoryResponse ToResponse(this TrackedRepository repository) =>
    new(repository.Id, repository.Name, repository.DefaultBranch);

  #endregion
}
```

Côté Web, même forme dans `CodeCoverage.Web.Mapping`, avec `ToViewModel` et
`Get{Thing}ViewModelAsync`.

---

## 8. Infrastructure et persistance (deux fournisseurs)

### 8.1 EF Core

- `CodeCoverageDbContext` (`Infrastructure/Persistence/`) : `sealed`, `DbSet` en `=> Set<T>()`,
  `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
- `Persistence/Configurations/{Entity}Configuration.cs` : `internal sealed class … :
  IEntityTypeConfiguration<T>` avec `#region Implementation of IEntityTypeConfiguration<T>` ; mappe
  les entités du Domain.
- `Persistence/Conventions/` : conventions communes aux deux fournisseurs (énumérations stockées en
  texte, convertisseurs JSON).
- `Persistence/Repositories/{Entity}Repository.cs` : `internal sealed`, implémente le port, Domain
  en entrée et en sortie, enregistre immédiatement ; enregistré dans
  `Infrastructure/DependencyInjection.Repositories.cs` (`AddRepositories`, §8.3).

### 8.2 Deux fournisseurs, toujours

Un seul modèle EF Core 10 pour **PostgreSQL et SQL Server**. Un assembly de migrations par
fournisseur : `Infrastructure.PostgreSql` et `Infrastructure.SqlServer`.

- Un changement de modèle s'accompagne de **deux migrations dans le même commit** ; un test par
  fournisseur échoue si le modèle a des changements sans migration
  (`Infrastructure.Tests/Persistence/ModelSnapshotTests.cs`).
- Les deux migrations se génèrent ensemble avec `pwsh scripts/new-migration.ps1 -Name AddThing` :
  même identifiant des deux côtés, en-têtes de fichier (§3.1), et rien de modifié si un côté échoue.
- **Train des migrations** : deux migrations ouvertes en même temps entrent en conflit sur les deux
  `CodeCoverageDbContextModelSnapshot.cs`. **Au plus une pull request contenant une migration est
  ouverte à la fois**, dans l'ordre de `docs/PLAN.md` ; la suivante s'ouvre une fois la précédente
  fusionnée dans `develop`. Après un rebase qui apporte une autre migration, régénérer la sienne :
  `pwsh scripts/new-migration.ps1 -Name AddThing -Regenerate`.
- Aucun SQL propre à un fournisseur hors des dialectes de la file de jobs
  (`PostgreSqlJobClaimDialect`, `SqlServerJobClaimDialect`, §9).
- Choix du moteur : `src/CodeCoverage.Infrastructure/Persistence/DbContextOptionsBuilderExtensions.cs`
  (`UseCodeCoverageDatabase(provider, connectionString)` : un `switch` sur `DatabaseProvider` qui
  pointe chaque moteur sur son assembly de migrations, nommé et non référencé).
- `DesignTimeDbContextFactory` `internal sealed` par projet de migrations ; chaîne de connexion de
  conception surchargeable par la variable `CODECOVERAGE_DESIGN_CONNECTION`.
- Migrations appliquées au démarrage par **l'Api seule** (`DatabaseMigrator`), sauf
  `CodeCoverage:Database:MigrateOnStartup=false` ; le Web ne migre jamais et reste « non prêt »
  tant que le schéma n'est pas à jour.

### 8.3 `AddInfrastructure`

`src/CodeCoverage.Infrastructure/DependencyInjection.cs` : options liées et validées au démarrage,
`AddDbContext<CodeCoverageDbContext>`, ports enregistrés en `TryAdd*` (`IClock` → `SystemClock`,
`ITokenHasher` → `Sha256TokenHasher`…), `TimeProvider.System`, health checks de disponibilité
marqués `ReadyTag`. Chaque fournisseur ajoute son dialecte par `AddPostgreSqlProvider()` /
`AddSqlServerProvider()` ; le dialecte configuré est choisi à l'exécution.

`DependencyInjection` est une `public static partial class` : le fichier ci-dessus garde le socle
commun et `AddInfrastructure` appelle une fois la méthode d'extension privée de chaque domaine,
dans un fichier voisin. Un nouvel adaptateur s'enregistre dans le fichier de son domaine, jamais
directement dans `AddInfrastructure` :

| Fichier (`src/CodeCoverage.Infrastructure/`) | Méthode | Enregistre |
|---|---|---|
| `DependencyInjection.Reports.cs` | `AddReports` | analyseurs de rapports et leurs caches |
| `DependencyInjection.GitHub.cs` | `AddGitHub` | passerelles de l'API GitHub, validation des webhooks |
| `DependencyInjection.Uploads.cs` | `AddUploads` | stockage des rapports envoyés, jetons d'envoi |
| `DependencyInjection.Repositories.cs` | `AddRepositories` | repositories de `Persistence/Repositories/` (§8.1) |
| `DependencyInjection.Notifications.cs` | `AddNotifications` | statuts et commentaires des pull requests |
| `DependencyInjection.Access.cs` | `AddAccess` | accès des utilisateurs aux dépôts |
| `DependencyInjection.Settings.cs` | `AddSettings` | options des groupes de paramètres (§12.4) |

### 8.4 Journalisation (Serilog) et télémétrie

`{Host}/Configuration/SerilogConfiguration.cs` : logger de bootstrap, `ReadFrom.Configuration` +
`ReadFrom.Services`, `Enrich.FromLogContext/WithMachineName/WithThreadId`, propriété
`Application`, console lisible en développement, `CompactJsonFormatter` ailleurs, fichier roulant
(`Logging:FilePath`). Traces et métriques OpenTelemetry, exportées en OTLP seulement si
`OTEL_EXPORTER_OTLP_ENDPOINT` est défini.

### 8.5 Health checks

`{Host}/Configuration/HealthProbes.cs` : `/health/live` (le processus répond, aucun check) et
`/health/ready` (checks marqués `ReadyTag` : base joignable, migrations appliquées…), anonymes et
exclus de la description OpenAPI.

### 8.6 En-têtes de sécurité

`{Host}/Configuration/Security/SecurityHeadersMiddleware.cs` : `X-Content-Type-Options: nosniff`,
`Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, CSP `frame-ancestors`.
Les en-têtes `X-Forwarded-*` ne sont acceptés que des proxies connus (`ForwardedHeaders:KnownProxies`).

---

## 9. Jobs en arrière-plan

Règle : **aucun travail lourd dans une requête HTTP**. Uploads et webhooks sont stockés, mis en file
dans la file de jobs en base, et traités par le worker de l'Api (ADR-005). Seule l'Api exécute le
worker (`Worker:Enabled`) ; le Web ne fait que mettre en file.

Pour ajouter un job :

1. une constante dans `src/CodeCoverage.Domain/Contracts/Jobs/JobTypes.cs` (jamais renommée une
   fois publiée : elle est stockée en texte, au plus `JobRequest.TypeMaxLength` caractères) ;
2. un cas dans `src/CodeCoverage.Infrastructure/Jobs/JobDispatcher.cs`, qui transforme la charge
   JSON en commande de l'Application et l'envoie par `ISender` (pipeline complet, en tant que
   principal système ; les commandes de job sont `ISystemOnly`) ;
3. la mise en file par la méthode de repository du changement, **dans sa transaction**, avec un
   `JobRequest` (`LockKey` pour sérialiser, `DedupKey` + `Coalesce` ou `MaxDelay` pour fusionner).

Un handler qui doit attendre lève `RetryLaterException` (§6.2).

---

## 10. Projet Api

### 10.1 Contrôleurs MVC (pas de minimal API)

Contrôleurs MVC `Api/Controllers/{Resource}Controller.cs` sous `/api/v1`. **Aucun mapping minimal
API** (`MapGet`, `MapGroup`) pour un endpoint métier : seuls ceux du framework (fichiers statiques,
health probes, OpenAPI, redirection Scalar) sont mappés.

Constantes de route : `src/CodeCoverage.Api/CodeCoverageApi.cs` (`Route = "api/v1"`,
`Prefix = "/" + Route`, `Tag = "CodeCoverage"`).

Exemple : `src/CodeCoverage.Api/Controllers/PingController.cs` :

```csharp
[ApiController]
[Route(CodeCoverageApi.Route + "/diagnostics")]
[Tags(CodeCoverageApi.Tag)]
public sealed class PingController(ISender sender) : ControllerBase
```

Conventions :

- `public sealed`, constructeur primaire avec `ISender`, `#region Methods` ;
- actions suffixées `Async`, `CancellationToken` en dernier paramètre ;
- `Name =` sur chaque route (`[HttpGet("ping", Name = "Ping")]`), utilisé par `CreatedAtRoute` ;
- `[ProducesResponseType<T>(StatusCodes.Status200OK)]` pour le succès et
  `[ProducesResponseType<ProblemDetails>(...)]` par statut d'erreur ;
- `<summary>` = titre court, `<remarks>` = description (§10.5) ;
- `[FromQuery]string? x` (sans espace) ; JSON avec énumérations en texte
  (`JsonStringEnumConverter`) ;
- un objet du Domain ne passe que par un mapper (§7).

### 10.2 Contrats

`public sealed record` positionnels documentés par `<param>`, dans `Api/Contracts/{Feature}/`
(exemple : `Api/Contracts/Diagnostics/PingResponse.cs`) : entrées `{Verb}{Thing}Request` /
`Save{Entity}Request`, sorties `{Entity}Response` / `{Thing}PageResponse`, objets imbriqués
`{Thing}Model`. Jamais de type du Domain dans un contrat.

### 10.3 Middleware d'exceptions

`src/CodeCoverage.Api/Configuration/Errors/ApiExceptionHandler.cs` : `IExceptionHandler` +
`IProblemDetailsService`.

- Un `switch` sur le type donne le statut et le titre (table du §6.2), `ApplicationException` en
  400 par défaut, toute autre exception en 500.
- ≥ 500 journalisé en `Error` avec l'exception, le reste en `Warning`.
- `Detail` = message de l'exception pour un refus seulement : le message d'un bug n'est jamais
  renvoyé.
- Extensions ProblemDetails : `errorCode` (tout refus) et `errors` (validation, par propriété).

Câblage (`src/CodeCoverage.Api/Program.cs`) : `AddProblemDetails()`,
`AddExceptionHandler<ApiExceptionHandler>()`, puis `app.UseExceptionHandler()` (sans chemin :
l'hôte Api ne sert pas de pages).

### 10.4 `Program.cs` et composition

- `Program.cs` enveloppé par Serilog : logger de bootstrap, `try`/`catch` qui journalise `Fatal`
  sauf `HostAbortedException`, `finally` `await Log.CloseAndFlushAsync()`.
- Une classe statique par préoccupation dans `Configuration/` (`AddApiDocumentation` /
  `UseApiDocumentation`, `MapScalarDocumentation`, `AddHealthProbes` / `MapHealthProbes`,
  `SerilogConfiguration`, `AddPresentation`, `AddTelemetry`, `AddForwardedHeaders`).
- Fin de fichier : `public partial class Program;` documentée, exposée pour
  `WebApplicationFactory`.
- Ordre du pipeline de l'Api : `UseForwardedHeaders` → `SecurityHeadersMiddleware` →
  `UseExceptionHandler` → documentation (si activée) → `UseRouting` → identité de la requête →
  `MapControllers` → `MapHealthProbes` → `MapScalarDocumentation` (si activée).

### 10.5 OpenAPI : Swagger + Scalar

Exigence utilisateur : Swagger **et** Scalar. Choix : **Swashbuckle** (génération du document et
Swagger UI) + **Scalar**.

- `src/CodeCoverage.Api/Configuration/OpenApiConfiguration.cs` : `AddApiDocumentation()`
  (`AddEndpointsApiExplorer`, `AddSwaggerGen` avec le document `v1`, la documentation XML de l'Api
  et de l'Application, le schéma de sécurité `Bearer` pour les jetons d'API et d'upload) et
  `UseApiDocumentation()` (document `/openapi/v1.json`, Swagger UI `/swagger`).
- `src/CodeCoverage.Api/Configuration/ScalarConfiguration.cs` : `MapScalarDocumentation()`
  (Scalar sur `/scalar/v1`, `/scalar` redirige ; client C# `HttpClient` par défaut ; schéma
  `Bearer` préféré).
- Documentation **désactivée en Production** sauf si `Features:EnableApiDocs` vaut `true`
  (`IsApiDocumentationEnabled()`).
- La « vraie documentation » vient de `GenerateDocumentationFile=true`, des `<summary>` /
  `<remarks>` des contrôleurs, des `[ProducesResponseType<…>]` et des `[Tags]` ; elle est vérifiée
  par `tests/CodeCoverage.Api.Tests/OpenApi/OpenApiDocumentTests.cs`.

---

## 11. Projet Web (Blazor Server)

### 11.1 Socle

- Blazor Server, rendu `InteractiveServer` ; pas de WebAssembly.
- Pipeline (`src/CodeCoverage.Web/Program.cs`) : `SecurityHeadersMiddleware` →
  `UseExceptionHandler("/error", true)` → `UseStatusCodePagesWithReExecute("/not-found")` →
  `UseRequestLocalization` (`en`, `fr`) → `UseAntiforgery` → `MapStaticAssets` →
  `MapHealthProbes` → `MapRazorComponents<App>()`.

### 11.2 Pages et ViewModels

- Pages : `Web/Components/Pages/{Name}.razor` (balisage et directives) + `{Name}.razor.cs` (tout le
  C#, classe partielle, régions du §3.3).
- Les pages ne manipulent que des **ViewModels** (`Web/ViewModels/{Feature}/{Thing}ViewModel`),
  obtenus par mappers (`Web/Mapping/{Feature}Mapper.cs`, `ToViewModel`,
  `Get{Thing}ViewModelAsync`, §7). Aucun type du Domain dans les composants.

### 11.3 Envoi des requêtes : `UiRequestRunner`

- Une page n'injecte **jamais** `ISender`, `IMediator` ni `ScopedSender` : elle envoie par
  `UiRequestRunner.RunAsync` (vérifié par `PresentationIsolationTests.WebComponents_ShouldNotHoldASender`).
- `ScopedSender` (`src/CodeCoverage.Web/Messaging/ScopedSender.cs`) ouvre un scope DI **par
  requête** (le scope d'un circuit vit aussi longtemps que l'onglet : un `DbContext` partagé
  servirait une opération à la fois et garderait ce qu'il a lu en premier) ; le scope est préparé
  par `ISendScopeInitializer`, qui copie l'utilisateur du circuit dans le principal du scope.
- `UiRequestRunner` (`src/CodeCoverage.Web/Messaging/UiRequestRunner.cs`, scoped) reçoit un
  `Func<ISender, CancellationToken, Task<T>>` (en général une extension de mapper) et rend un
  `UiResult<T>` (`Succeeded` + `Value`, `NotFound`, ou refusé et déjà signalé).

### 11.4 Gestion des erreurs côté Blazor

Mécanisme central (exigence utilisateur, voir §16.3 point 4) :

- `UiRequestRunner` :
  - `ValidationException` avec un formulaire : messages posés sur les champs de l'`EditContext`
    (par nom de propriété) ;
  - `NotFoundException` : résultat `NotFound`, la page affiche son état vide « introuvable ou
    inaccessible » (`CcEmptyState`) ;
  - autre `ApplicationException` : toast de danger (`IToastService`) dont le message est la
    traduction de l'`ErrorCode` ;
  - tout le reste (bug, annulation) remonte.
- `CcErrorBoundary` dans `MainLayout`, réinitialisé sur `NavigationManager.LocationChanged`
  (`_errorBoundary?.Recover()`).
- `UseExceptionHandler("/error", true)` et page `/error` pour le rendu serveur ;
  `/not-found` pour les 404.

### 11.5 Localisation

- Aucune chaîne d'interface en dur : `IStringLocalizer<Common>` (injecté sous le nom `L`),
  ressources `Web/Resources/Common.resx` (anglais) + `Common.fr.resx` ; les libellés des composants
  UI maison dans `UiStrings.resx` / `UiStrings.fr.resx`.
- Les codes d'erreur sont traduits sous leur propre nom par
  `src/CodeCoverage.Web/Localization/ErrorCodeLocalizer.cs` ; un code sans traduction retombe sur
  `General.Unexpected` : l'utilisateur ne voit jamais un code brut ni le message technique de
  l'exception.
- Des tests vérifient que les deux langues ont les mêmes clés (`Web.Tests/Resources/`).
- Dans ces quatre fichiers, chaque entrée tient sur une seule ligne
  (`<data …><value>…</value></data>`), ce que vérifie un test : ils fusionnent en `merge=union`
  (`.gitattributes`), et des entrées sur plusieurs lignes s'entremêleraient à la fusion. Un retour
  à la ligne dans une valeur s'écrit `&#10;`.

### 11.6 UI maison

Pas de bibliothèque de composants externe : composants maison dans `Web/Components/Ui`, préfixe
`Cc` (`CcAppShell`, `CcButton`, `CcToast`, `CcErrorBoundary`…), modèles dans
`Web/Components/Ui/Models`, services dans `Web/Components/Ui/Services` (`IToastService`), icônes
dans `Web/Components/Ui/Icons`, classes CSS et variables préfixées `cc-` / `--cc-`. La référence
visuelle est `docs/design/ui-mockup.html` ; polices auto-hébergées.

---

## 12. Paramètres rechargeables à chaud

### 12.1 Exigence

Exigence utilisateur : éviter les paramètres dans `appsettings.json`, privilégier des paramètres
**enregistrés en base, modifiés dans la console web et rechargés à chaud** sans redémarrage. Aucun
paramètre fonctionnel dans `appsettings.json`.

### 12.2 Mécanisme

| Élément | Emplacement |
|---|---|
| `SettingDefinition` (clé, groupe, type, défaut, `RequiresRestart`, min/max, secret) et le catalogue `SettingDefinitions` | `Domain/Settings` |
| `ISettingsStore` (valeur effective + source, enregistrement, réinitialisation) | `Domain/Ports` |
| `DatabaseSettingsConfigurationSource` / `…Provider` (lecture ADO.NET brute, la configuration étant construite avant la DI ; table absente → aucun réglage) | `Infrastructure/Settings` |
| `ConfigurationBuilderExtensions.AddCodeCoverageSettingsStore` (source **la plus faible**) | `Infrastructure/Settings` |
| `ConfigurationSettingsStore` (EF, enregistrement puis `RefreshAll`) | `Infrastructure/Settings` |
| `ConfigurationSourceInspector` (quelle source fournit la clé) | `Infrastructure/Settings` |
| `SettingsChangeWatcher` (propagation entre hôtes, §12.3) | `Infrastructure/Settings` |
| `UpdateSetting`, `ResetSetting`, `ReplaceSecret`, `GetSettings` | `Application/Features/Settings` |
| Secrets : Data Protection (`ISecretProtector`), jamais renvoyés à l'UI (« Remplacer », jamais « Afficher ») | `Infrastructure/Security` |

Priorité : ligne de commande > variables d'environnement > `appsettings.json` > store > défaut du
catalogue. Une valeur fournie par une source plus forte est en lecture seule dans l'écran et
l'API (`Setting.ProvidedByEnvironment`, 422).

### 12.3 Propagation entre les deux hôtes

ADR-006 : une table à une ligne `SettingsRevision` ; chaque enregistrement incrémente la révision
dans la même transaction ; dans chaque hôte, `SettingsChangeWatcher` (`PeriodicTimer` de
`Settings:PollSeconds`, 5 s par défaut) relit la révision et recharge le provider si elle a changé.
L'hôte qui enregistre se rafraîchit immédiatement, l'autre suit en 5 s au plus. Pas de
`LISTEN/NOTIFY` (non portable).

### 12.4 Consommation

Options liées par
`services.AddOptions<XxxOptions>().Bind(configuration.GetSection(XxxOptions.SectionName)).ValidateOnStart();`
et lues par **`IOptionsMonitor<XxxOptions>`**, jamais `IOptions<T>` pour un réglage modifiable.
Un réglage `RequiresRestart` est signalé dans l'UI.

### 12.5 Amorçage (environnement uniquement)

Restent dans l'environnement (ou `appsettings.json`) : `ConnectionStrings:CodeCoverage`,
`CodeCoverage:Database:Provider`, `CodeCoverage:Database:MigrateOnStartup`, `Logging:FilePath`,
`Features:EnableApiDocs`, `Worker:Enabled`, `Settings:PollSeconds`, `DataProtection:*`,
`ForwardedHeaders:KnownProxies`, `CodeCoverage:Bootstrap:AdminLogins`. La liste complète est dans
`docs/ARCHITECTURE.md` §4bis.2. Tout le reste passe par le store.

---

## 13. Tests

### 13.1 Outils

xUnit v3, Shouldly, FakeItEasy, `using` globaux (§2.6) ; un projet de tests par couche, plus
`Uploader.Tests` et `Architecture.Tests` (NetArchTest et analyse de source Roslyn) ;
Testcontainers pour la base ; `WebApplicationFactory` pour démarrer les hôtes (`Api.Tests`,
`Web.Tests`) ; bUnit pour les composants ; Verify pour les snapshots (commentaires de PR,
`*.verified.txt` en LF) ; analyseurs et totaux vérifiés contre les fixtures de `tests/fixtures/`.

### 13.2 Organisation et nommage

- Dossiers miroirs de la couche testée : `Application.Tests/Behaviors/`,
  `Application.Tests/Features/{Feature}/Commands/{UseCase}/`, utilitaires partagés dans `Support/`.
- Classe : `public sealed class {Subject}Tests`, namespace file-scoped miroir du dossier, en-tête
  de fichier, régions, **pas** de documentation XML.
- Jeton d'annulation xUnit v3 :
  `private static CancellationToken Ct => TestContext.Current.CancellationToken;` (dans
  `#region Properties`).
- Nommage : **`{Method}_{Scenario}_Should{Behavior}`** (le scénario peut être omis quand il n'y en
  a qu'un), par exemple `Handle_WhenProvidedByEnvironment_ShouldThrowUnprocessableEntity`.

Exemple : `tests/CodeCoverage.Application.Tests/Features/Settings/Commands/UpdateSetting/UpdateSettingCommandHandlerTests.cs`.

### 13.3 Tests de base de données

`Infrastructure.Tests` exécute chaque test de base sur **PostgreSQL et SQL Server**
(`[ClassData(typeof(ProviderTheoryData))]`, construit à partir de l'énumération
`DatabaseProvider`). Sans Docker, ces tests sont **ignorés avec une raison explicite**
(`Assert.Skip`), jamais en échec (`Support/DatabaseFixture.cs`) ; de même dans `Api.Tests` et
`Web.Tests`.

### 13.4 Tests d'architecture

`tests/CodeCoverage.Architecture.Tests` garde, dès le départ :

| Test | Règle | Section |
|---|---|---|
| `LayerDependencyTests` | dépendances entre couches ; générateur Mediator dans Application seule | §1.2, §5.5 |
| `FeatureLayoutTests` | `Features/{Feature}/{Commands|Queries}/{UseCase}/`, noms d'après le dossier, aucun `Dto` | §5.1 |
| `HandlerConventionTests` | handlers `internal sealed` | §3.5 |
| `PortConventionTests` | ports sans `IQueryable`, `CancellationToken` obligatoire, pas d'unité de travail | §4.2 |
| `PresentationIsolationTests` | aucun type Domain dans l'Api et le Web hors `Mapping`, `Program`, `Configuration` ; aucun sender dans les composants | §7, §11.3 |
| `SourceFileConventionTests` | en-tête, région `Ctors`, un type par fichier | §3.1, §3.3, §3.5 |

Chaque règle a aussi un test négatif sur des échantillons (`Samples/`) qui prouve qu'elle détecte
une violation. **Les garder vertes, ne jamais les assouplir pour faire passer un changement.**

---

## 14. Docker et CI

### 14.1 Workflow

`.github/workflows/ci-cd.yml` : déclencheurs `push` sur `main`, `develop` et tags `v*.*.*`, `pull_request` ;
jobs `gitversion` (GitVersion 6.x, `gittools/actions/gitversion/setup@v4` + `execute@v4`) →
`build-and-test` (`actions/checkout@v6` `fetch-depth: 0`, `actions/setup-dotnet@v5` avec
`global.json`, `dotnet build -c Release -p:NuGetVersion=… -p:AssemblySemVer=…
-p:MajorMinorPatch=… -p:InformationalVersion=…`, `dotnet test --no-build`, Testcontainers PostgreSQL
et SQL Server) → test de fumée compose → images Docker Api et Web poussées sur GHCR. Ce dernier job
ne tourne que sur `main` (`latest`) et les tags `v*` ; sur `develop` et les PR, le test de fumée
compose construit déjà les deux images (§15).
Aucune étape de flux NuGet privé. Images `${IMAGE_PREFIX}-api` et `${IMAGE_PREFIX}-web`, avec
`IMAGE_PREFIX` = `ghcr.io/<owner>/code-coverage` en CI et `code-coverage` par défaut en local.

### 14.2 Couverture

Le projet définit sa propre étape d'upload de couverture (action composite et template Azure
fournis par ce dépôt, jalon M5).

### 14.3 Dockerfiles

`Dockerfile.api` et `Dockerfile.web` : multi-étapes `mcr.microsoft.com/dotnet/sdk:10.0` →
`aspnet:10.0`, restauration depuis nuget.org seul (ni secret BuildKit ni configuration NuGet
privée), version injectée par `ARG`, publication en dossier, utilisateur et groupe non root
`codecoverage` (uid/gid 1001, `groupadd`/`useradd`), volume `/app/data`, port 8080, configuration
d'amorçage par variables `Section__Key`.

### 14.4 docker-compose

`docker-compose.yml` : `name: code-coverage` ; ports publiés sur la boucle locale seulement
(`"127.0.0.1:${API_PORT:-8081}:8080"`, `"127.0.0.1:${WEB_PORT:-8080}:8080"`, reverse proxy TLS devant, voir `Caddyfile.example`) ;
`postgres:17-alpine` avec healthcheck `pg_isready`, `depends_on: condition: service_healthy`,
`restart: unless-stopped`. Variante SQL Server par surcouche `docker-compose.sqlserver.yml`.

---

## 15. Branches, commits et checklist

### 15.1 Branches (GitFlow)

Le dépôt suit GitFlow, aligné sur `GitVersion.yml` (`workflow: GitFlow/v1`) :

| Branche | Rôle | Version GitVersion | Images publiées |
|---|---|---|---|
| `develop` | intégration : tout le travail y arrive par pull request | `x.y.z-beta.n` | non |
| `feature/{jalon}-{sujet}` | un jalon ou un sujet, partie de `develop`, fusionnée dans `develop` | `-alpha` | non |
| `release/x.y.z` | stabilisation d'une version, partie de `develop`, fusionnée dans `main` et `develop` | `-rc` | non |
| `hotfix/x.y.z` | correctif urgent, partie de `main`, fusionnée dans `main` et `develop` | `-hotfix` | non |
| `main` | versions livrées uniquement, taguées `vX.Y.Z` | `x.y.z` | oui (`latest` et la version) |

- On ne commite jamais directement sur `main` : elle ne reçoit que des fusions de `release/*` et
  `hotfix/*`, suivies du tag `vX.Y.Z`.
- **Une issue GitHub par micro-feature** du plan (créées au démarrage de chaque jalon, jalon GitHub
  et labels `layer: …`, lien `Issue` sous le titre de la micro-feature dans `docs/PLAN.md`). Sa
  branche s'appelle `feature/{id}-{sujet}` (`feature/m1.01-coverage-math`) et sa pull request vers
  `develop` contient `Closes #{issue}`. Un travail hors plan (outillage, docs) ouvre sa propre issue.
- La CI tourne sur `develop`, `main`, les tags et toutes les pull requests ; les images Docker
  sont construites par le test de fumée compose partout, et poussées sur GHCR par le job `docker`,
  qui ne tourne que sur `main` et les tags `v*` (§14.1).

### 15.2 Commits et checklist

- **Une micro-fonctionnalité de `docs/PLAN.md` = un commit = une PR de quelques fichiers**, rien
  d'une micro-fonctionnalité suivante.
- Message en anglais, conventional commit + gitmoji, sans clé de ticket :
  `feat(app): ✨ Add the HTTP-like exception hierarchy`.
- **Checklist avant commit** :
  - `dotnet build -c Release` sans avertissement, `dotnet test` vert ;
  - en-têtes, documentation XML, régions (`Ctors`), un type par fichier (profil
    « CodeCoverage: Full Cleanup ») ;
  - nouveau code d'erreur : `{Feature}Errors.cs`, `Common.resx` et `Common.fr.resx` ;
  - changement de modèle : deux migrations, test « pas de changement de modèle sans migration »
    vert ;
  - revue du `git diff`.

---

## 16. Synthèse des choix, écarts assumés et lacunes

### 16.1 Synthèse des choix

| Sujet | Choix du projet |
|---|---|
| Hôtes | Deux projets, `Api` et `Web`, deux hôtes de la même Application (exigence utilisateur, ADR-002) |
| Médiateur | martinothamar `Mediator` 3.x, pas MediatR (exigence utilisateur, ADR-001) |
| Validation | FluentValidation 12, `ValidationException` du projet, 400 |
| Découpage Application | `Features/{Feature}/Commands|Queries/{UseCase}/` (exigence utilisateur) |
| Ports et contrats | `Domain/Ports/`, `Domain/Contracts/{Area}/` (exigence utilisateur) |
| Retour des requêtes | Objets du Domain ; refus par exceptions ; ni DTO ni `Result<T>` |
| Exceptions | Hiérarchie `ApplicationException(ErrorCode)` avec 400, 401, 403, 404, 409, 422, 502 |
| Codes d'erreur | `Feature.Reason` |
| Handler d'exceptions Api | `IExceptionHandler` + ProblemDetails (`errorCode`, `errors`) |
| Exceptions Blazor | `UiRequestRunner` + `CcErrorBoundary` |
| Mappers | Méthodes d'extension explicites dans l'Api et le Web ; aucun type Domain dans les contrats et les vues |
| OpenAPI | Swashbuckle + Swagger UI + Scalar (exigence utilisateur) |
| Paramètres | Provider de configuration sur table + `IOptionsMonitor`, propagation par révision (ADR-006) |
| Persistance | Un modèle EF Core 10, PostgreSQL et SQL Server, deux assemblies de migrations |
| Tenancy | Instance mono-tenant (ADR-003) |
| Région des constructeurs | `Ctors` |
| Visibilité des handlers | `internal sealed` |
| `ConfigureAwait` | `false` partout, `true` dans les `.razor.cs` et `UiRequestRunner` |
| Ligne vide après l'en-tête | Oui |
| Nommage des tests | `{Method}_{Scenario}_Should{Behavior}` |
| `NuGetAuditLevel` | `moderate` |
| Central Package Management | Non |

### 16.2 Écarts assumés

1. **`Handle` / `ValueTask<T>`** dans les handlers et behaviors au lieu du suffixe `Async` et de
   `Task<T>`, imposés par Mediator (§5.5).
2. **Documentation XML non bloquante au build** : `CS1591` est dans `NoWarn` ; la règle est tenue
   par la revue (§2.5, §3.6).
3. **Régions non créées par l'outillage** : écrites à la main ; seuls le nom `Ctors` et l'en-tête
   sont vérifiés automatiquement (§3.7).
4. **`RetryLaterException`** dérive de `Exception`, pas d'`ApplicationException` : ce n'est pas un
   refus (§6.2).
5. **Générateur de source dans une bibliothèque** (Application) plutôt que dans l'exécutable,
   contrairement à la recommandation de Mediator ; validé par le spike M0.07 et par
   `MediatorWiringTests` (ADR-001).

### 16.3 Lacunes tranchées et décisions ouvertes

Points qui ont demandé une décision avant implémentation :

1. **API et vues dans deux projets** — tranché (ADR-002) : le Web référence l'Application et envoie
   les requêtes via `ISender` en processus (mêmes validations, mêmes permissions), jamais d'appel
   HTTP à l'Api.
2. **Alternative à MediatR** — tranché (ADR-001) : `Mediator` 3.x. `IDomainEvent` remplace
   `INotification` dans le Domain ; pas d'événements de domaine au MVP.
3. **Exceptions HTTP complètes** — tranché : ajout de `UnauthorizedException` (401) et
   `BadGatewayException` (502) ; validation en 400 (§6).
4. **Gestion centrale des exceptions côté Blazor** — tranché : `UiRequestRunner`, `CcErrorBoundary`
   réinitialisé à la navigation, page `/error` pour le rendu serveur (§11.4).
5. **Aucun objet du Domain dans les vues** — tranché : ViewModels et mappers du Web, protégés par
   un test d'architecture (§7, §11.2).
6. **Swagger + Scalar** — tranché : Swashbuckle + Swagger UI + Scalar (§10.5).
7. **Paramètres rechargeables avec deux hôtes** — tranché (ADR-006) : interrogation périodique de
   `SettingsRevision` (§12.3).
8. **Couverture de code en CI** — le projet la définit lui-même (§14.2, jalon M5).
9. **Multi-tenant** — tranché (ADR-003) : instance mono-tenant.
10. **Nom du produit et du dépôt** — tranché : produit `CodeCoverage`, dépôt `code-coverage`,
    projet indépendant ; l'organisation GitHub servie n'est pas fixée (`GitHub:Organization` et
    `Uploads:AllowedOwners` sans valeur par défaut, saisis dans la console web).
11. **Application des régions par l'outillage** — ouvert : aucun File Layout ReSharper ; l'ordre
    des régions est vérifié en revue.
