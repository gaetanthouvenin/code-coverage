---
source-sha256: 646e28cc7146d4505bd421d0837b4230598a3079a5f48b15faf1c98106c5582d
---
# Migrer un dépôt depuis Codecov

Ce guide fait passer les pipelines d’un dépôt de Codecov à une instance CodeCoverage. Il suppose que
l’organisation est déjà accueillie (la GitHub App installée et l’organisation admise, voir
[le guide d’accueil d’une organisation](operations/organization-onboarding.md)). Le `codecov.yml` du
dépôt reste tel quel : CodeCoverage le lit (variantes `codecov.yml`, `.codecov.yml`, `.github/` et
`dev/`) avec la même sémantique.

Dans les commandes ci-dessous, `https://coverage-api.example.com` est l’adresse publique de l’Api de votre
instance et `https://coverage.example.com` celle de la console Web (deux noms d’hôte, ADR-011).

## 1. Pendant le pilote : envoyer aux deux services

Gardez l’étape Codecov et ajoutez à côté une étape CodeCoverage, pour que les deux services voient chaque commit.
CodeCoverage publie ses statuts sous les noms `coverage/project` et `coverage/patch` tant que votre organisation
garde le préfixe par défaut `coverage` : ils n’entrent jamais en collision avec `codecov/*`. `tools/rollout/Add-CoverageUpload.ps1`
ouvre ces pull requests pour vous.

### GitHub Actions (OIDC, sans secret)

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write          # the upload authenticates with the OIDC token of the job
    steps:
      - uses: actions/checkout@v6          # a shallow clone is enough: the diff comes from GitHub
      # ... build and test ...
      - uses: codecov/codecov-action@v5    # kept during the pilot
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
      - uses: gaetanthouvenin/code-coverage/action@v0.1.0-beta.2   # @vX.Y.Z: no @v1 before the first stable release
        if: ${{ !cancelled() }}
        with:
          version: 0.1.0-beta.2            # the same release of CodeCoverage
          sha256: <SHA-256 of coverage-upload-linux-x64 of that release>
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

L’action télécharge l’uploader de la release épinglée, vérifie son SHA-256, et ne fait jamais échouer le build
(un avertissement, code de sortie 0) sauf avec `fail-on-error: true`. Voir [action/README.md](../action/README.md).

Sans l’action, la même chose dans une étape simple (le binaire de la release, sa somme vérifiée) :

```yaml
      - name: Upload coverage to CodeCoverage
        if: ${{ !cancelled() }}
        env:
          VERSION: 1.2.3
          SHA256: <SHA-256 of coverage-upload-linux-x64>
          COVERAGE_URL: https://coverage-api.example.com
          COVERAGE_FLAGS: unit
        run: |
          curl -fsSL -o coverage-upload "https://github.com/<owner>/code-coverage/releases/download/v$VERSION/coverage-upload-linux-x64"
          echo "$SHA256  coverage-upload" | sha256sum -c - && chmod +x coverage-upload && ./coverage-upload
```

### Azure Pipelines

```yaml
resources:
  repositories:
    - repository: coverage
      type: github
      name: <owner>/code-coverage
      endpoint: <GitHub service connection>
      ref: refs/tags/v1.2.3

variables:
  - group: coverage            # COVERAGE_TOKEN (secret), UPLOADER_SHA256_LINUX_X64...

steps:
  # ... build and test ...
  - template: pipelines/templates/coverage-upload.yml@coverage
    parameters:
      version: 1.2.3
      url: https://coverage-api.example.com
      files: $(Agent.TempDirectory)/**/coverage.cobertura.xml
      flags: unit
```

Le template mappe `COVERAGE_TOKEN` explicitement (Azure Pipelines ne transmet jamais un secret implicitement), vérifie
la somme de la release et celle épinglée dans le groupe de variables (`UPLOADER_SHA256_{RID}`), et avertit
au lieu d’échouer. Les builds Azure Pipelines d’un dépôt hébergé sur GitHub sont pris en charge ; les dépôts
hébergés sur Azure Repos ne le sont pas, avant la forge Azure DevOps (ADR-014, M7). Voir
[pipelines/demo/azure-pipelines.yml](../pipelines/demo/azure-pipelines.yml).

### Tout le reste : curl

```bash
git ls-files > network.txt
curl -fsS --retry 5 -X POST "https://coverage-api.example.com/api/v1/uploads" \
  -H "Authorization: Bearer $COVERAGE_TOKEN" \
  -F slug=my-org/my-repo -F sha="$COMMIT_SHA" -F branch="$BRANCH" -F pr="$PR_NUMBER" \
  -F flags=frontend -F network=@network.txt -F file=@coverage/lcov.info || true
```

### L’outil dotnet

Sur un agent avec .NET 10 : `dotnet tool install --global CodeCoverage.Uploader`, puis `coverage-upload`.
Chaque option se rabat sur une variable `COVERAGE_*` (`--flags` sur `COVERAGE_FLAGS`, `--file` sur
`COVERAGE_FILE`, fichiers séparés par `;`).

## 2. Jetons

- **OIDC (recommandé sur GitHub Actions) :** rien à stocker. Le jeton du job prouve le
  dépôt ; l’instance fait confiance à l’identifiant numérique du compte de l’organisation admise.
- **Jetons d’upload :** gardez vos valeurs de `CODECOV_TOKEN`. Un administrateur d’organisation les importe, et
  les pipelines gardent leur secret inchangé (l’uploader lit `COVERAGE_TOKEN`, puis `CODECOV_TOKEN`) :
  - console : `/orgs/{forge}/{org}/upload-tokens` (jeton d’organisation) ou les réglages d’un dépôt ;
  - API : `POST /api/v1/orgs/{forge}/{org}/upload-tokens` ou `POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens`
    avec `{"label": "...", "importedValue": "<CODECOV_TOKEN>"}` et un jeton d’API `cvp_` d’un administrateur ;
  - script : `tools/rollout/Import-CodecovTokens.ps1` (à blanc par défaut, lit un CSV conservé hors du
    dépôt).
  Une valeur ne peut être importée qu’une fois par organisation ; un second import répond 409.
- **Badges des dépôts privés :** importez le « graphing token » de Codecov comme jeton de badge, pour que
  les URL de badge de vos README ne changent que d’hôte : `PUT /api/v1/repos/{forge}/{owner}/{repo}/badge-token`
  avec `{"value": "<graphing token>"}`, ou les réglages du dépôt dans la console.

## 3. Monorepos et plusieurs uploads par commit

- `after_n_builds` par flag (`flags.<name>.after_n_builds`) fonctionne comme dans Codecov. Un
  `codecov.notify.after_n_builds` global fonctionne aussi, mais compte chaque upload du commit : avec des jobs qui
  envoient des flags différents, préférez la forme par flag.
- Quand le dernier job sait que tous les uploads sont terminés, il ajoute `--finalize` à son propre upload : l’uploader
  envoie ses rapports, puis déclare les uploads du commit complets, ce qui notifie aussitôt, sans tenir compte de
  `after_n_builds` ni du debounce. `--finalize` n’agit qu’après un upload de la même exécution ; un job
  sans rapport appelle `POST /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete`
  avec les mêmes identifiants que les uploads. C’est l’équivalent de `upload-complete` de Codecov.
- `manual_trigger: true` attend cet appel.

## 4. Badges

Changez l’hôte des URL de badge, rien d’autre : `https://codecov.io/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`
devient `https://coverage-api.example.com/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`.
Le segment de service `github` et la variante `graphs` sont acceptés aussi. La console Web donne les
snippets Markdown et HTML de chaque dépôt.

## 5. Historique

Avant de résilier Codecov, importez les totaux que Codecov conserve pour chaque commit, pour que le graphique d’historique
remonte avant la migration : `tools/rollout/Start-CodecovImport.ps1` appelle
`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import` avec un jeton d’API Codecov, que l’instance
garde protégé jusqu’à la fin de l’import (sept jours au plus). L’import lit l’API de Codecov, que
la résiliation ferme : lancez-le d’abord.

## 6. La bascule

Quand la parité du pilote est expliquée (`tools/parity`, [parity-log.md](parity-log.md)), un
administrateur d’organisation bascule l’organisation : les notifications de Codecov arrêtées, puis
`Notifications:StatusContextPrefix` réglé sur `codecov` (et `Notifications:ChecksEnabled` activé) dans les
paramètres de l’organisation. Les statuts gardent les noms qu’exigent vos protections de branche. La
séquence complète se trouve dans [le runbook de bascule](cutover-runbook.md).

## Différences avec Codecov

Chaque différence est nommée ici. Celles qui tiennent à l’isolation entre organisations (ADR-013) et à
l’identité multi-forge (ADR-014) ne sont pas configurables, par conception ; les autres le sont.

| Différence | Configurable | Pourquoi |
|---|---|---|
| L’App doit être installée et l’organisation admise avant que les uploads soient acceptés (401 `Upload.TokenInvalid` pour un jeton, `Repository.NotFound` pour OIDC ; l’uploader sort quand même avec 0). | Non (ADR-013) | L’admission est fermée. |
| Pas de jeton d’upload global : un jeton ne fonctionne que dans l’organisation qui l’a créé ou importé. | Non (ADR-013) | Isolation. |
| Un dépôt transféré démarre avec un historique vide, aucun jeton d’upload et un nouveau jeton de badge. | Non (ADR-013) | L’ancien historique reste dans l’ancienne organisation. |
| Après le renommage du compte GitHub, l’ancien propriétaire ne se résout plus (les badges affichent `unknown`) ; aucune redirection. | Non (ADR-013) | Un login réutilisé ne doit jamais atteindre l’ancienne organisation. |
| L’API de gestion et les pages Web portent la forge dans leurs chemins (`/api/v1/repos/github/...`, `/github/my-org/my-repo`) ; l’API d’upload et les URL de badge gardent les formes de Codecov. | Non (ADR-014) | Plusieurs forges partagent une même instance. |
| Les statuts sont nommés `coverage/*` jusqu’à ce que l’organisation règle `Notifications:StatusContextPrefix` sur `codecov`. | Oui, par organisation | Le pilote et Codecov tournent côte à côte. |
| Des statuts, pas des Check Runs, jusqu’à ce que l’organisation active `Notifications:ChecksEnabled` (après avoir accepté la permission Checks de l’App) ; ensuite des Check Runs avec les mêmes noms et des annotations de patch, comme Codecov. | Oui, par organisation | Chaque organisation accorde la permission à son propre rythme. |
| Valeurs par défaut d’un dépôt sans `codecov.yml` : le profil historique (statuts project et patch), pas les valeurs par défaut centrées sur le patch de Codecov en 2024. | Oui (`codecov.yml`, ou le YAML de l’organisation) | ADR-009, PLAN §11-7. |
| Historique importé : les commits importés de Codecov n’ont que des totaux (ni fichiers, ni sessions) ; leurs pages n’affichent aucun fichier, seul le graphique d’historique les montre, et ils ne sont jamais comparés. | Non (l’API v2 ne donne que des totaux) | M5.06. |
| `comment.behavior: spammy` et `new` peuvent publier plus souvent des commentaires identiques : CodeCoverage notifie de nouveau quand un rapport de base arrive en retard, lors du balayage et lors d’une re-notification manuelle. | Non | La fraîcheur plutôt que le silence. |
| `comment.behavior: new` : quand GitHub refuse de supprimer le commentaire précédent (403, 422), CodeCoverage le journalise et publie quand même le nouveau commentaire, si bien que l’ancien peut rester à côté. Codecov s’arrête probablement à la place (`no_permissions`) ; ce n’est pas prouvé. | Non | Conservé jusqu’à ce que le comportement de Codecov soit vérifié (ARCHITECTURE §8.4). |
| `codecov/codecov-action` et codecov-cli ne dialoguent pas avec CodeCoverage (le shim de compatibilité est reporté jusqu’à ce que le pilote mesure le besoin). | — | M5.07. |

## Le rapport détaillé

Le bouton **Rapport détaillé** du panneau Fichiers d’un commit ouvre
`/{forge}/{owner}/{repo}/commit/{sha}/report` : une ligne par fichier, le total en tête, et la même vue
pour un flag. La couverture Codecov reste le chiffre de tête, celui des statuts, du commentaire et des
badges ; le rapport y ajoute trois chiffres, que Codecov n’a pas. Aucun ne change un statut.

### Les quatre chiffres

| Chiffre | Couvert | Couvrable |
|---|---|---|
| **Codecov** | lignes couvertes | lignes couvertes, partielles et manquées : `hits / (hits + partielles + manquées)` |
| **Lignes** | lignes exécutées au moins une fois, partielles comprises, comme dans ReportGenerator | lignes du rapport |
| **Branches** | branches prises | branches des lignes |
| **Méthodes** | méthodes exécutées au moins une fois | méthodes du rapport |

Une ligne à branches exécutée sans qu’aucune branche soit prise est manquée pour Codecov et exécutée pour
**Lignes** : les deux chiffres diffèrent par les lignes partielles. Chaque pourcentage est arrondi selon
`coverage.precision` et `coverage.round` du `codecov.yml`, comme le chiffre Codecov. Un clic sur un en-tête
de colonne trie les fichiers, un second clic inverse l’ordre ; **Afficher** garde les fichiers sous
l’objectif du statut projet, ceux qui ont une branche non prise ou ceux qui ont une méthode jamais
exécutée. Le flag, le tri et le filtre sont dans l’adresse : un lien partagé ouvre la même vue.

### Ce que signifie un tiret

`—` signifie qu’il n’y a rien à couvrir (un fichier sans branche), ou que le rapport ne donne pas cette
donnée : les méthodes d’un format ou d’un upload qui n’en a pas. Un nombre de méthodes inconnu n’est jamais
affiché 0 : un fichier fusionné depuis une partie qui connaît ses méthodes et une partie qui ne les connaît
pas affiche `—`, et le total aussi.

### Un commit traité avant le rapport

Un commit traité avant la version qui a apporté le rapport ne garde que les compteurs de Codecov : la page
affiche un bandeau et `—` dans toutes les colonnes sauf Codecov. **Retraiter**, proposé à un administrateur
du dépôt, reconstruit le détail depuis les uploads bruts, conservés 30 jours (`Retention:RawDays`). Le
prochain upload de la branche l’a sans rien faire.

### Écarts avec ReportGenerator

Les chiffres suivent les rapports tels que Codecov en lit les lignes : ils peuvent donc différer de
ReportGenerator. Chaque écart est nommé et n’est pas configurable (ADR-016) :

| Écart | Pourquoi |
|---|---|
| Un rapport LCOV d’un fichier `.js` n’a aucune branche. | Codecov ignore ses enregistrements `BRDA`. |
| Une ligne changée en ligne simple par `parsers.*.partials_as_hits` perd ses branches. | Comme chez Codecov. |
| Une ligne `cond` de Clover compte deux branches, vrai et faux. | Le format ne donne pas d’autre compte. |
| Deux méthodes qui commencent sur la même ligne (une lambda, une méthode synthétique) comptent pour une. | Une méthode est gardée par sa première ligne. |
| Une méthode Cobertura est couverte dès qu’une de ses lignes l’est. | Le format n’a pas de compteur de méthode. |
| Les « méthodes entièrement couvertes » de ReportGenerator ne sont pas calculées. | Hors du rapport. |
