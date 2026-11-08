# ADR-012 : Uploader distribué en binaires servis par l'Api

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §5.2, §5.3, §5.4, §5.5, §5.6 et §12.1 ; `docs/PLAN.md` M1 (uploader) et M5

## Contexte

- Les pipelines à migrer tournent sur GitHub Actions et Azure Pipelines ; certains jobs n'ont pas de SDK .NET (par exemple des jobs JavaScript).
- Un outil `dotnet tool` exige un SDK .NET et l'accès à un flux NuGet, que tous les agents n'ont pas.
- L'outillage de couverture ne doit jamais casser un build (principe non négociable n°5), et le binaire exécuté en CI doit être vérifiable (chaîne d'approvisionnement).

## Décision

- L'uploader `coverage-upload` est publié en **binaires autonomes mono-fichier** (`--self-contained`, `PublishSingleFile`, `PublishTrimmed`) pour `linux-x64`, `linux-musl-x64`, `win-x64` et `osx-arm64`.
- Ces binaires sont **embarqués dans l'image Api** et servis anonymement par `GET /api/v1/uploader/{rid}`, avec leur somme par `GET /api/v1/uploader/{rid}.sha256`. Les mêmes binaires et sommes sont joints aux releases GitHub.
- Les templates CI **épinglent une version et vérifient la somme SHA-256** avant l'exécution.
- L'**outil dotnet** (`PackAsTool`, `net8.0;net10.0`, `RollForward=Major`) est publié **en complément** sous le package `CodeCoverage.Uploader`, sur un flux NuGet choisi en M5 (nuget.org public ou flux privé, PLAN §11-11).
- Une recette `curl` reste documentée pour les cas sans binaire.

## Conséquences

- **Positives :**
  - aucun prérequis .NET ni accès au flux NuGet sur l'agent CI ;
  - la version de l'uploader suit celle du serveur qui la sert ;
  - intégrité vérifiable par la somme épinglée.
- **Négatives :**
  - l'image Api grossit des quatre binaires ;
  - la publication multi-RID et le trimming ajoutent une étape au build de l'image et de la release ;
  - l'Uploader ne référence aucun projet de la solution : son seul couplage avec le serveur est le contrat HTTP, qui doit rester rétrocompatible.
