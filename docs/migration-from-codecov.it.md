---
source-sha256: 1017bca7ee8867d68b1870da6385f7ac4e0614ff9838d3986a279b3acd887306
---
# Migrare un repository da Codecov

Questa guida sposta le pipeline di un repository da Codecov a un’istanza CodeCoverage. Presuppone che
l’organizzazione abbia già completato l’onboarding (la GitHub App installata e l’organizzazione ammessa, vedere
[la guida all’onboarding delle organizzazioni](operations/organization-onboarding.md)). Il `codecov.yml` del
repository resta com’è: CodeCoverage lo legge (`codecov.yml`, `.codecov.yml`, varianti `.github/` e
`dev/`) con la stessa semantica.

Nei comandi qui sotto, `https://coverage-api.example.com` è l’indirizzo pubblico dell’Api della vostra
istanza e `https://coverage.example.com` quello della console Web (due nomi host, ADR-011).

## 1. Durante il pilota: upload su entrambi i servizi

Mantenete lo step di Codecov e aggiungete accanto uno step di CodeCoverage, in modo che entrambi i servizi vedano ogni commit.
CodeCoverage pubblica i suoi stati come `coverage/project` e `coverage/patch` finché la vostra organizzazione
mantiene il prefisso predefinito `coverage`: non entrano mai in conflitto con `codecov/*`. `tools/rollout/Add-CoverageUpload.ps1`
apre queste pull request per voi.

### GitHub Actions (OIDC, nessun secret)

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

L’action scarica l’uploader della release fissata, ne verifica lo SHA-256 e non fa mai fallire la build
(un avviso, codice di uscita 0) a meno di `fail-on-error: true`. Vedere [action/README.md](../action/README.md).

Senza l’action, lo stesso in uno step semplice (il binario della release, con la sua somma verificata):

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

Il template mappa `COVERAGE_TOKEN` esplicitamente (Azure Pipelines non passa mai un secret implicitamente), verifica
la somma della release e quella fissata nel variable group (`UPLOADER_SHA256_{RID}`), ed emette un avviso
invece di fallire. Le build Azure Pipelines di un repository ospitato su GitHub sono supportate; i repository
ospitati su Azure Repos non lo sono, prima della forge Azure DevOps (ADR-014, M7). Vedere
[pipelines/demo/azure-pipelines.yml](../pipelines/demo/azure-pipelines.yml).

### Qualsiasi altra cosa: curl

```bash
git ls-files > network.txt
curl -fsS --retry 5 -X POST "https://coverage-api.example.com/api/v1/uploads" \
  -H "Authorization: Bearer $COVERAGE_TOKEN" \
  -F slug=my-org/my-repo -F sha="$COMMIT_SHA" -F branch="$BRANCH" -F pr="$PR_NUMBER" \
  -F flags=frontend -F network=@network.txt -F file=@coverage/lcov.info || true
```

### Il dotnet tool

Su un agent con .NET 10: `dotnet tool install --global CodeCoverage.Uploader`, poi `coverage-upload`.
Ogni opzione ricade su una variabile `COVERAGE_*` (`--flags` su `COVERAGE_FLAGS`, `--file` su
`COVERAGE_FILE`, file separati da `;`).

## 2. Token

- **OIDC (consigliato su GitHub Actions):** nulla da memorizzare. Il token del job dimostra il
  repository; l’istanza si fida dell’id numerico dell’account dell’organizzazione ammessa.
- **Token di upload:** conservate i vostri valori `CODECOV_TOKEN`. Un amministratore dell’organizzazione li importa, e
  le pipeline mantengono il loro secret invariato (l’uploader legge `COVERAGE_TOKEN`, poi `CODECOV_TOKEN`):
  - console: `/orgs/{forge}/{org}/upload-tokens` (token dell’organizzazione) o le impostazioni di un repository;
  - API: `POST /api/v1/orgs/{forge}/{org}/upload-tokens` o `POST /api/v1/repos/{forge}/{owner}/{repo}/upload-tokens`
    con `{"label": "...", "importedValue": "<CODECOV_TOKEN>"}` e un token API `cvp_` di un amministratore;
  - script: `tools/rollout/Import-CodecovTokens.ps1` (dry run per impostazione predefinita, legge un CSV tenuto fuori dal
    repository).
  Un valore può essere importato una sola volta per organizzazione; una seconda importazione risponde 409.
- **Badge dei repository privati:** importate il «graphing token» di Codecov come token del badge, in modo che
  gli URL dei badge dei vostri README cambino solo l’host: `PUT /api/v1/repos/{forge}/{owner}/{repo}/badge-token`
  con `{"value": "<graphing token>"}`, oppure le impostazioni del repository nella console.

## 3. Monorepo e più upload per commit

- `after_n_builds` per flag (`flags.<name>.after_n_builds`) funziona come in Codecov. Anche un
  `codecov.notify.after_n_builds` globale funziona, ma conta ogni upload del commit: con job che
  caricano flag diversi, preferite la forma per flag.
- Quando l’ultimo job sa che tutti gli upload sono terminati, aggiunge `--finalize` al proprio upload: l’uploader
  invia i suoi report, poi dichiara completi gli upload del commit, il che notifica subito, ignorando
  `after_n_builds` e il debounce. `--finalize` agisce solo dopo un upload della stessa esecuzione; un job
  senza report chiama `POST /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}/uploads-complete`
  con la stessa credenziale degli upload. È l’equivalente di `upload-complete` di Codecov.
- `manual_trigger: true` attende quella chiamata.

## 4. Badge

Cambiate l’host degli URL dei badge, nient’altro: `https://codecov.io/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`
diventa `https://coverage-api.example.com/gh/my-org/my-repo/branch/main/graph/badge.svg?token=...`.
Sono accettati anche il segmento di servizio `github` e la variante `graphs`. La console Web fornisce gli
snippet Markdown e HTML di ciascun repository.

## 5. Storico

Prima di disdire Codecov, importate i totali che Codecov conserva per ogni commit, in modo che il grafico dello storico
risalga a prima della migrazione: `tools/rollout/Start-CodecovImport.ps1` chiama
`POST /api/v1/repos/{forge}/{owner}/{repo}/codecov-import` con un token API di Codecov, che l’istanza
conserva protetto finché l’importazione non termina (sette giorni al massimo). L’importazione legge l’API di Codecov, che
la disdetta chiude: eseguitela prima.

## 6. Il passaggio

Quando la parità del pilota è spiegata (`tools/parity`, [parity-log.md](parity-log.md)), un
amministratore dell’organizzazione effettua il passaggio dell’organizzazione: notifiche di Codecov interrotte, poi
`Notifications:StatusContextPrefix` impostato a `codecov` (e `Notifications:ChecksEnabled` attivato) nelle
impostazioni dell’organizzazione. Gli stati mantengono i nomi richiesti dalle vostre branch protection. La sequenza
completa si trova nel [runbook del passaggio](cutover-runbook.md).

## Differenze con Codecov

Ogni differenza è indicata qui. Quelle dell’isolamento tra organizzazioni (ADR-013) e dell’identità
multi-forge (ADR-014) non sono configurabili per scelta progettuale; le altre sì.

| Differenza | Configurabile | Perché |
|---|---|---|
| L’App deve essere installata e l’organizzazione ammessa prima che gli upload siano accettati (401 `Upload.TokenInvalid` per un token, `Repository.NotFound` per OIDC; l’uploader esce comunque con 0). | No (ADR-013) | L’ammissione è chiusa. |
| Nessun token di upload globale: un token funziona solo nell’organizzazione che lo ha creato o importato. | No (ADR-013) | Isolamento. |
| Un repository trasferito parte con uno storico vuoto, nessun token di upload e un nuovo token del badge. | No (ADR-013) | Il vecchio storico resta alla vecchia organizzazione. |
| Dopo la ridenominazione dell’account GitHub, il vecchio owner non viene più risolto (i badge indicano `unknown`); nessun reindirizzamento. | No (ADR-013) | Un login riutilizzato non deve mai raggiungere la vecchia organizzazione. |
| L’API di gestione e le pagine Web riportano la forge nei loro percorsi (`/api/v1/repos/github/...`, `/github/my-org/my-repo`); l’API di upload e gli URL dei badge mantengono le forme di Codecov. | No (ADR-014) | Più forge condividono un’istanza. |
| Gli stati si chiamano `coverage/*` finché l’organizzazione non imposta `Notifications:StatusContextPrefix` a `codecov`. | Sì, per organizzazione | Il pilota e Codecov funzionano fianco a fianco. |
| Stati, non Check Run, finché l’organizzazione non attiva `Notifications:ChecksEnabled` (dopo aver accettato il permesso Checks dell’App); poi Check Run con gli stessi nomi e annotazioni della patch, come Codecov. | Sì, per organizzazione | Ogni organizzazione concede il permesso al proprio ritmo. |
| Valori predefiniti di un repository senza `codecov.yml`: il profilo storico (stati project e patch), non i valori predefiniti di Codecov del 2024 centrati sulla patch. | Sì (`codecov.yml`, o lo YAML dell’organizzazione) | ADR-009, PLAN §11-7. |
| Storico importato: i commit importati da Codecov hanno solo i totali (nessun file, nessuna sessione); le loro pagine non mostrano alcun file, solo il grafico dello storico li mostra, e non vengono mai confrontati. | No (l’API v2 fornisce solo i totali) | M5.06. |
| `comment.behavior: spammy` e `new` possono pubblicare commenti identici più spesso: CodeCoverage notifica di nuovo quando arriva un report di base in ritardo, durante lo sweep e su una nuova notifica manuale. | No | Freschezza piuttosto che silenzio. |
| `comment.behavior: new`: quando GitHub rifiuta di eliminare il commento precedente (403, 422), CodeCoverage lo registra nei log e pubblica comunque il nuovo commento, quindi quello vecchio può restare accanto. Codecov probabilmente si ferma invece (`no_permissions`); ciò non è dimostrato. | No | Mantenuto finché il comportamento di Codecov non è verificato (ARCHITECTURE §8.4). |
| `codecov/codecov-action` e codecov-cli non comunicano con CodeCoverage (lo shim di compatibilità è rinviato finché il pilota non misura il bisogno). | — | M5.07. |
