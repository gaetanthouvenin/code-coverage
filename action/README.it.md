---
source-sha256: 53c1dd5f4fd143fb494bfc1a6009be8cdd8e672e18a115e2e1c503ac797f9ecb
---
# Action di upload di CodeCoverage

Un’action composita che scarica l’uploader standalone di CodeCoverage di una **release fissata**,
ne verifica lo SHA-256 e carica i report di copertura su un’istanza CodeCoverage. Solo Bash, nessuna
action di terze parti; ogni input raggiunge gli script tramite `env:`.

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

Fissare l’action su un tag di release, `@vX.Y.Z`, e dare lo stesso numero a `version`: l’esempio usa
`v0.1.0-beta.2`. Non esiste ancora un tag major come `@v1`: l’owner lo imposta alla prima release
stabile. La panoramica di un repository senza copertura, nella console, mostra questo job con la
versione e l’indirizzo dell’Api dell’istanza, pronto da copiare.

## Input

| Input | Obbligatorio | Predefinito | Significato |
|---|---|---|---|
| `version` | sì | | La release dell’uploader, senza la `v` (`1.2.3` per il tag `v1.2.3`). |
| `sha256` | no | | Lo SHA-256 che il binario del runner deve avere. Il `.sha256` della release viene sempre verificato; una somma fissata protegge anche da una release sostituita. |
| `url` | sì | | L’indirizzo pubblico dell’Api di CodeCoverage. |
| `token` | no | | Un token di upload (`CODECOV_TOKEN` continua a funzionare una volta importato). Vuoto: OIDC, che richiede `id-token: write`. |
| `files` | no | | File di report, separati da `;` o da a capo. Vuoto: viene esplorato il workspace. |
| `flags` | no | | Flag, separati da virgole. |
| `name` | no | | Il nome dell’upload. |
| `dry-run` | no | `false` | Prepara e stampa l’upload senza inviarlo. |
| `fail-on-error` | no | `false` | Fa fallire lo step quando l’installazione o l’upload falliscono. |
| `download-url` | no | `https://github.com/<repository of the action>/releases/download` | Dove vengono letti i file della release. Il repository dell’action proviene da `github.action_repository`, o dal percorso di checkout dell’action quando quello è vuoto. |

Output `installed`: `true` quando l’uploader è stato scaricato e la sua somma verificata.

## Runner supportati

`linux-x64` (glibc), `linux-musl-x64` (container Alpine), `win-x64` e `osx-arm64`. Un altro runner
riceve un avviso e nessun upload.

## I fallimenti non rompono mai la build

Un download fallito, una somma che non corrisponde o un runner non supportato scrivono un `::warning::` che indica
la causa e terminano lo step con codice di uscita 0; lo step di upload viene saltato. Anche l’uploader stesso esce
con 0 in caso di upload fallito. Impostare `fail-on-error: true` per fallire invece.

`v0.1.0-beta.1` segnalava una somma diversa su ogni runner Windows e non caricava nulla: la somma veniva
calcolata da un nome di file sotto `RUNNER_TEMP` (`D:\a\_temp\…`), di cui `sha256sum` esegue l’escape con un
`\` iniziale. Da `v0.1.0-beta.2` il binario viene letto da stdin, quindi l’avviso significa solo che i byte
differiscono (M4.02g).

## Da dove proviene il binario

I binari e i loro `.sha256` sono quelli della release GitHub della versione fissata, estratti
dall’immagine dell’Api dello stesso tag (ADR-012, modificato per M5.01): la versione e la somma cambiano
insieme, quando il workflow viene aggiornato. La route `/api/v1/uploader/{rid}` di un’istanza resta
disponibile come ripiego e per l’uso manuale, ma serve la versione del server, quindi una somma fissata
in un workflow smetterebbe di corrispondere al successivo aggiornamento del server.

## Differenze con `codecov/codecov-action`

- Il binario è l’uploader di CodeCoverage, non la Codecov CLI; `codecov/codecov-action` continua a funzionare
  solo con Codecov (lo shim codecov-cli, M5.07, è rinviato).
- Nessun interruttore `use_oidc`: un `token` vuoto significa OIDC.
- `url` è obbligatorio: non esiste un’istanza predefinita.
- Il tag mobile `v1` viene spostato a mano dai maintainer sull’ultima release `v1.x.y`.
