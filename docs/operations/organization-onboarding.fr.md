---
source-sha256: 73f1dd082c287d4922d9bf73ab66f3b052f44fb78c286fced2d9be7b50470848
---
# Accueillir une organisation

Comment un compte GitHub (une organisation ou un utilisateur) rejoint une instance CodeCoverage partagée par plusieurs
organisations isolées (ADR-013). L’administrateur d’instance l’admet ; un administrateur d’organisation
la configure ensuite. Rien d’une organisation n’est visible depuis une autre.

## 1. Installer l’App

Un propriétaire du compte GitHub installe la GitHub App de l’instance depuis
`https://github.com/apps/<slug>/installations/new`, sur tous ses dépôts ou sur une sélection. L’administrateur
d’instance trouve ce lien dans l’en-tête de `/admin/organizations` (et à la fin de
`/setup`), et l’envoie au propriétaire. L’App demande :

| Permission | Pourquoi |
|---|---|
| Contents: read, Metadata: read | Le diff, les arbres, le `codecov.yml`, la source annotée. |
| Commit statuses: read & write | Les statuts `coverage/*` (puis `codecov/*`). |
| Pull requests: read & write | Les données de pull request et le commentaire. |
| Checks: read & write | Les Check Runs, une fois que l’organisation active `Notifications:ChecksEnabled` (M5.05). Une App créée avant M5.05 l’obtient quand les mainteneurs l’ajoutent ; chaque installation l’accepte ensuite depuis GitHub. |
| Organization members: read | Les rôles de l’organisation (admin, membre) dans la console. |

L’installation crée une organisation **en attente** : elle ne suit encore rien, et tout upload est refusé
(401 `Upload.TokenInvalid` avec un jeton, `Repository.NotFound` avec OIDC ; l’uploader sort quand même avec 0).
GitHub renvoie ensuite le propriétaire vers la console. Si l’organisation en attente n’apparaît pas (son
webhook a été perdu), l’administrateur d’instance clique sur **Vérifier les installations** dans `/admin/organizations` :
le worker liste aussitôt les installations de l’App, au lieu d’attendre l’exécution nocturne.

## 2. L’admettre

L’administrateur d’instance admet l’organisation en attente dans `/admin/organizations` (**Admettre** sur sa
ligne), ou admet un compte avant son installation avec **Admettre un compte** : il saisit le login, la
console lit l’identifiant du compte sur GitHub (`GET /users/{login}`) et affiche « login → id » pour
confirmation. Un login inconnu est refusé (`Organization.UnknownGitHubLogin`) et rien n’est enregistré.
L’administrateur qui admet voit l’organisation aussitôt, sans se reconnecter. En secours,
sans la console (docs/operations.md, « La ligne de commande d’administration ») :

```bash
dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
```

L’identifiant du compte est l’identifiant numérique du compte GitHub (`gh api users/my-org --jq .id`). L’admission
lance la synchronisation des dépôts de l’installation.

## 3. La configurer

Un administrateur de l’organisation (un propriétaire de l’organisation GitHub, ou l’utilisateur d’un compte
personnel) ouvre `/orgs/github/<org>/settings`. Les cinq paramètres d’une organisation :

| Paramètre | Défaut | Quand le changer |
|---|---|---|
| `Notifications:StatusContextPrefix` | `coverage` | `codecov` à la bascule (docs/cutover-runbook.md). |
| `Notifications:StatusesEnabled` | `true` | Désactivé pour que CodeCoverage reste silencieux sur GitHub. |
| `Notifications:CommentsEnabled` | `true` | Désactivé pour laisser seuls les commentaires de pull request de Codecov pendant le pilote. |
| `Notifications:ChecksEnabled` | `false` | Activé à la bascule, une fois que l’installation a accepté la permission Checks. |
| `Coverage:OrganizationYaml` | aucun | Un YAML partagé par tous les dépôts (sous le `codecov.yml` propre à chaque dépôt). |

## 4. Jetons et badges

- **OIDC** ne demande rien : un job GitHub Actions avec `id-token: write` prouve son dépôt.
- **Jetons d’upload :** importez les valeurs de `CODECOV_TOKEN` (organisation et dépôts) dans
  `/orgs/github/<org>/upload-tokens`, par l’API, ou avec `tools/rollout/Import-CodecovTokens.ps1`.
  Une valeur n’est unique qu’au sein de l’organisation.
- **Jetons d’API** (`cvp_…`, pour les scripts) : chaque utilisateur les crée dans la console ; ils agissent avec les
  droits de leur utilisateur, dans une seule organisation.
- **Jetons de badge** des dépôts privés : importez le graphing token de Codecov dans les réglages de chaque
  dépôt, ou renouvelez-le.

## 5. Ce qu’une organisation ne peut pas configurer (ADR-013)

Ces points découlent de l’isolation des organisations et sont les mêmes pour tous :

- l’App doit être installée et l’organisation admise avant que quoi que ce soit ne soit accepté ;
- pas de jeton d’upload global : un jeton ne fonctionne que dans l’organisation qui l’a créé ou importé ;
- un dépôt transféré repart de zéro dans sa nouvelle organisation (historique vide, aucun jeton d’upload, nouveau
  jeton de badge) ; son historique reste dans l’ancienne organisation ;
- après le renommage du compte GitHub, l’ancien login ne se résout plus, et rien ne redirige ;
- l’administrateur d’instance ne voit que des métadonnées (`/admin/operations`, `/admin/organizations`), jamais
  la couverture d’une organisation ; les exploitants de la base de données, des sauvegardes et de la supervision voient
  tout (docs/operations.md).

Les logins réservés de la première conception (un login égal à un segment de route du produit) ont disparu :
le propriétaire n’est plus le premier segment d’une route (ADR-014).

## 6. Partir

Désinstaller l’App suspend l’organisation : ses données sont conservées (pas encore d’effacement), ses uploads sont
refusés, et la réinstallation par le même compte la rattache de nouveau.
