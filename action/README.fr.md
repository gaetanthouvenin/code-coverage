---
source-sha256: 53c1dd5f4fd143fb494bfc1a6009be8cdd8e672e18a115e2e1c503ac797f9ecb
---
# Action d’upload CodeCoverage

Une action composite qui télécharge l’uploader autonome de CodeCoverage d’une **release épinglée**,
vérifie son SHA-256, et envoie des rapports de couverture à une instance CodeCoverage. Bash uniquement, aucune
action tierce ; chaque entrée parvient aux scripts par `env:`.

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write   # the upload authenticates with the OIDC token of GitHub Actions
    steps:
      - uses: actions/checkout@v6
      # ... build and test, producing coverage.cobertura.xml ...
      - uses: gaetanthouvenin/code-coverage/action@v0.1.0-beta.2
        with:
          version: 0.1.0-beta.2
          sha256: <SHA-256 of coverage-upload-linux-x64 of v0.1.0-beta.2>   # optional, recommended
          url: https://coverage-api.example.com
          files: coverage.cobertura.xml
          flags: unit
```

Épinglez l’action sur un tag de release, `@vX.Y.Z`, et donnez le même numéro à `version` : l’exemple utilise
`v0.1.0-beta.2`. Il n’existe pas encore de tag majeur tel que `@v1` : le propriétaire le pose à la première release
stable. La vue d’ensemble d’un dépôt sans couverture, dans la console, montre ce job avec la
version et l’adresse de l’Api de l’instance, prêt à copier.

## Entrées

| Entrée | Obligatoire | Défaut | Signification |
|---|---|---|---|
| `version` | oui | | La release de l’uploader, sans le `v` (`1.2.3` pour le tag `v1.2.3`). |
| `sha256` | non | | Le SHA-256 que doit avoir le binaire du runner. Le `.sha256` de la release est toujours vérifié ; une somme épinglée protège en plus contre une release remplacée. |
| `url` | oui | | L’adresse publique de l’Api CodeCoverage. |
| `token` | non | | Un jeton d’upload (`CODECOV_TOKEN` continue de fonctionner une fois importé). Vide : OIDC, qui demande `id-token: write`. |
| `files` | non | | Fichiers de rapport, séparés par `;` ou par des retours à la ligne. Vide : l’espace de travail est parcouru. |
| `flags` | non | | Flags, séparés par des virgules. |
| `name` | non | | Le nom de l’upload. |
| `dry-run` | non | `false` | Prépare et affiche l’upload sans l’envoyer. |
| `fail-on-error` | non | `false` | Fait échouer l’étape quand l’installation ou l’upload échoue. |
| `download-url` | non | `https://github.com/<repository of the action>/releases/download` | L’endroit où les fichiers de la release sont lus. Le dépôt de l’action vient de `github.action_repository`, ou du chemin de checkout de l’action quand celui-ci est vide. |

Sortie `installed` : `true` quand l’uploader a été téléchargé et sa somme vérifiée.

## Runners pris en charge

`linux-x64` (glibc), `linux-musl-x64` (conteneurs Alpine), `win-x64` et `osx-arm64`. Un autre runner
reçoit un avertissement et aucun upload n’a lieu.

## Les échecs ne cassent jamais le build

Un téléchargement en échec, une somme qui ne correspond pas ou un runner non pris en charge écrit un `::warning::` qui nomme
la cause et termine l’étape avec le code de sortie 0 ; l’étape d’upload est ignorée. L’uploader lui-même sort
aussi avec 0 en cas d’échec de l’upload. Réglez `fail-on-error: true` pour échouer à la place.

`v0.1.0-beta.1` signalait une somme différente sur chaque runner Windows et n’envoyait rien : la somme était
calculée à partir d’un nom de fichier sous `RUNNER_TEMP` (`D:\a\_temp\…`), que `sha256sum` échappe avec un
`\` initial. Depuis `v0.1.0-beta.2`, le binaire est lu sur stdin, si bien que l’avertissement signifie seulement que les octets
diffèrent (M4.02g).

## D’où vient le binaire

Les binaires et leur `.sha256` sont ceux de la release GitHub de la version épinglée, extraits
de l’image de l’Api du même tag (ADR-012, amendé pour M5.01) : la version et la somme changent
ensemble, lors de la mise à jour du workflow. La route `/api/v1/uploader/{rid}` d’une instance reste
disponible en solution de repli et pour un usage manuel, mais elle sert la version du serveur, si bien qu’une somme épinglée
dans un workflow cesserait de correspondre à la prochaine mise à niveau du serveur.

## Différences avec `codecov/codecov-action`

- Le binaire est l’uploader de CodeCoverage, pas la CLI Codecov ; `codecov/codecov-action` ne continue de fonctionner
  qu’avec Codecov (le shim codecov-cli, M5.07, est reporté).
- Pas d’option `use_oidc` : un `token` vide signifie OIDC.
- `url` est obligatoire : il n’y a pas d’instance par défaut.
- Le tag mobile `v1` est déplacé à la main par les mainteneurs vers la dernière release `v1.x.y`.
