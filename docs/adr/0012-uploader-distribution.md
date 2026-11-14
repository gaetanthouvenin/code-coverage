# ADR-012 : Uploader distribué en binaires servis par l'Api

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §5.2, §5.3, §5.4, §5.5, §5.6 et §12.1 ; `docs/PLAN.md` M1 (uploader), M4.01 et M5
- **Amendé :** 2026-11-13 (la release GitHub, source principale pour la CI)

## Contexte

- Les pipelines à migrer tournent sur GitHub Actions et Azure Pipelines ; certains jobs n'ont pas de SDK .NET (par exemple des jobs JavaScript).
- Un outil `dotnet tool` exige un SDK .NET et l'accès à un flux NuGet, que tous les agents n'ont pas.
- L'outillage de couverture ne doit jamais casser un build (principe non négociable n°5), et le binaire exécuté en CI doit être vérifiable (chaîne d'approvisionnement).

## Décision

- L'uploader `coverage-upload` est publié en **binaires autonomes mono-fichier** (`--self-contained`, `PublishSingleFile`, `PublishTrimmed`) pour `linux-x64`, `linux-musl-x64`, `win-x64` et `osx-arm64`.
- Ces binaires sont **embarqués dans l'image Api** et servis anonymement par `GET /api/v1/uploader/{rid}`, avec leur somme par `GET /api/v1/uploader/{rid}.sha256`. Les mêmes binaires et sommes sont joints aux releases GitHub.
- Les templates CI **épinglent une version et vérifient la somme SHA-256** avant l'exécution.
- L'**outil dotnet** (`PackAsTool`, `net10.0` seul, `RollForward=Major`) est publié **en complément** sous le package `CodeCoverage.Uploader`, sur un flux NuGet choisi en M5 (nuget.org public ou flux privé, PLAN §11-11). Il est réservé aux agents disposant de .NET 10 ou ultérieur : les agents sans .NET 10 utilisent les binaires autonomes.
- Une recette `curl` reste documentée pour les cas sans binaire.

## Conséquences

- **Positives :**
  - aucun prérequis .NET ni accès au flux NuGet sur l'agent CI ;
  - la version de l'uploader suit celle du serveur qui la sert ;
  - intégrité vérifiable par la somme épinglée.
- **Négatives :**
  - l'outil dotnet n'est pas utilisable sur un agent sans .NET 10, qui doit passer par les binaires autonomes ;
  - l'image Api grossit des quatre binaires ;
  - la publication multi-RID et le trimming ajoutent une étape au build de l'image et de la release ;
  - l'Uploader ne référence aucun projet de la solution : son seul couplage avec le serveur est le contrat HTTP, qui doit rester rétrocompatible.

## Amendement du 2026-11-13 (M5.01, M5.02)

Décision du propriétaire, prise avec le lot M4/M5. La décision initiale était incohérente : l'Api ne sert que les binaires de sa propre version, donc une somme épinglée dans un template ne correspondrait plus après la mise à jour du serveur, et l'avertissement suivi d'un code 0 arrêterait les uploads en silence.

- La **release GitHub** de la version épinglée est la **source principale** des binaires pour la CI : l'action composite (`action/`, M5.01) et le template Azure (`pipelines/templates/coverage-upload.yml`, M5.02) téléchargent `coverage-upload-{rid}[.exe]` et son `.sha256` depuis la release `v{version}`, et vérifient la somme (et une somme épinglée par l'appelant, si elle est fournie). Version et somme changent ensemble, quand le workflow est mis à jour.
- Les binaires de la release sont **extraits de l'image Api** du même tag (M4.01), jamais recompilés : ils sont identiques à ceux que l'instance sert.
- La route `/api/v1/uploader/{rid}` de l'instance **reste**, documentée comme secours et pour l'usage manuel (recette `curl`, agent sans accès à GitHub).
- La conséquence « la version de l'uploader suit celle du serveur qui la sert » ne vaut plus pour la CI : la version suit l'épinglage du workflow ; le contrat HTTP reste rétrocompatible.
- L'outil dotnet est publié sur **nuget.org** par publication de confiance (OIDC), sous licence Apache-2.0 (PLAN §11-11, M5.03).
- Le tag majeur mobile `v1` de l'action est déplacé à la main par le propriétaire ; GitVersion l'ignore (vérifié le 2026-11-14 : un tag `v1` ne change pas la version calculée sur `main` ni sur `develop`, un tag `v0.1.0` la change).
