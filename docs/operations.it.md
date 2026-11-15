---
source-sha256: 42896000cd089e40af399044d62a735b21641e19e3270b61ff650300edd42ec4
---
# Operazioni

Come gestire un’istanza CodeCoverage in produzione. Il progetto alla base di questi passi si trova in
[ARCHITECTURE.md](ARCHITECTURE.md) §12 (in francese); questa guida è il punto di vista dell’operatore.

## Alert

Gli host esportano le loro metriche solo tramite OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`, un valore di bootstrap;
meter `CodeCoverage`, ARCHITECTURE §12.6). [`deploy/observability`](../deploy/observability) le trasforma
in alert Prometheus:

- `alerts.yml`: le regole, con le loro soglie (mai nella configurazione degli host);
- `alerts.test.yml`: i loro test unitari, eseguiti dalla CI con `promtool test rules`;
- `docker-compose.observability.yml`: un overlay che aggiunge un OpenTelemetry Collector (OTLP in ingresso,
  exporter Prometheus in uscita) e Prometheus, entrambi fissati per digest:

  ```bash
  docker compose -f docker-compose.yml -f deploy/observability/docker-compose.observability.yml up -d
  ```

  Gli alert attivi sono elencati su <http://localhost:9090/alerts>.

L’overlay non invia alcuna notifica. Collegare Prometheus al ricevitore scelto (Alertmanager con
mail o chat, un Prometheus ospitato) al di fuori di questo repository: gli URL e le password dei ricevitori sono secret
e non finiscono mai in questi file.

Le metriche non sono suddivise per organizzazione (ADR-013): lo stack di monitoraggio vede i nomi dei repository di
tutte le organizzazioni (`uploads_received_total{repo}`) e deve essere riservato agli operatori
dell’istanza, come i backup del database.

### Gli alert

| Alert | Espressione | Significato | Prima diagnosi |
|---|---|---|---|
| `CodeCoverageDeadJobs` | `max(jobs_dead) > 0` per 1 min | Un job ha esaurito tutti i suoi tentativi: la notifica, il parsing o l’importazione che portava non è mai avvenuto. | `/admin/operations`, leggere l’ultimo errore del job morto, correggere la causa, poi riprovarlo (`POST /api/v1/ops/jobs/{id}/retry`). I job morti restano finché non vengono riprovati o eliminati dopo `Retention:JobDays`. |
| `CodeCoverageJobQueueStalled` | `max(job_oldest_queued_age_seconds) > 600` per 2 min | Il job eseguibile più vecchio attende da più di 10 minuti. | È in esecuzione un’Api con `Worker:Enabled`? I suoi log, il carico del database. |
| `CodeCoverageWorkerHeartbeatStale` | `max(worker_heartbeat_age_seconds) > 120` per 1 min | Nessun worker ha dato segni di vita da 2 minuti (un worker invia un heartbeat ogni 15 s e scrive `WorkerHeartbeats`). | Avviare un’Api con il worker, o capire perché si è fermato. |
| `CodeCoverageWorkerHeartbeatMissing` | nessun `worker_heartbeat_age_seconds` mentre i gauge dei job sono esportati, per 5 min | Nessun worker ha mai inviato un heartbeat, o il suo ultimo heartbeat è stato eliminato (più vecchio di un giorno), situazione in cui la regola stale si risolve. | Avviare un’Api con il worker, o capire perché si è fermato. |
| `CodeCoverageMetricsAbsent` | `absent_over_time(job_oldest_queued_age_seconds[5m])` | Nessuna Api ha esportato i gauge dei job da 5 minuti. | Tutte le Api ferme, oppure l’endpoint OTLP, il collector o Prometheus guasti. |
| `CodeCoverageWebhookSignatureFailures` | `sum(increase(webhook_signature_failures_total[10m])) > 0`, oppure una serie assente 10 min fa e ora sopra 0 | Consegne di webhook con una firma non valida. | `GitHub:WebhookSecret` disallineato rispetto all’App dopo una rotazione, o richieste contraffatte. |
| `CodeCoverageGitHubQuotaLow` | rimanente registrato / limite `< 0.10` per installazione, per 5 min | Resta meno del 10 % della quota GitHub di un’installazione. | I job che chiamano GitHub attendono il reset senza perdere un tentativo; cercare un picco (rielaborazione, sweep). |

### Nomi delle metriche

Il collector esporta i nomi del meter così come sono (`add_metric_suffixes: false`): i nomi
riportano già la loro unità (`_seconds`) e `_total`. Un gauge letto dal database (`jobs_dead`,
`worker_heartbeat_age_seconds`, `jobs_queued`, `job_oldest_queued_age_seconds`) viene campionato ogni 30 s
da ciascuna Api, solo mentre un listener raccoglie i gauge dei job. Lo stesso vale per `github_ratelimit_limit` e
`github_ratelimit_recorded_remaining` (label `installation`): entrambi provengono dalle stesse righe di `GitHubQuotas`,
e una quota il cui orario di reset è passato conta come piena. `github_ratelimit_remaining` riporta
l’ultima risposta letta da questo processo; l’alert non lo usa. I contatori si azzerano quando un host si riavvia: le regole usano `increase()`.

### Verificare le regole

```bash
docker run --rm --entrypoint promtool -v "$PWD/deploy/observability:/rules:ro" -w /rules prom/prometheus:v3.5.0 test rules alerts.test.yml
```

### Provocare gli alert su uno stack locale

Con l’overlay in esecuzione (e `OTEL_METRIC_EXPORT_INTERVAL` a 10 s, come lo imposta l’overlay):

| Alert | Come |
|---|---|
| `CodeCoverageDeadJobs` | Inserire una riga di `Jobs` nello stato `Dead`, o lasciare che un job fallisca il suo ultimo tentativo. |
| `CodeCoverageWebhookSignatureFailures` | `POST /api/v1/github/webhooks` con un `X-Hub-Signature-256` errato (401). |
| `CodeCoverageWorkerHeartbeatStale` | Riavviare l’Api con `Worker__Enabled=false`. |
| `CodeCoverageWorkerHeartbeatMissing` | Avviare un’istanza nuova con `Worker__Enabled=false` (nessun heartbeat viene mai scritto). |
| `CodeCoverageJobQueueStalled` | Senza worker, accodare un job il cui `RunAfter` risale a più di 10 minuti fa. |
| `CodeCoverageMetricsAbsent` | Fermare l’Api e il Web (il collector conserva una serie per 5 minuti, poi la regola attende altri 5). |
| `CodeCoverageGitHubQuotaLow` | Richiede risposte di GitHub con un `x-ratelimit-remaining` basso; coperto da `alerts.test.yml`. |

## Produzione su una VM

[`deploy/vm`](../deploy/vm) esegue l’intera istanza su un solo host Docker (M4.02): Caddy (TLS, ACME),
PostgreSQL 17, l’Api (API REST, job worker, migrazioni), il Web (console) e un container di backup.
Solo immagini: l’Api e il Web provengono dal registry della CI alla versione di una release, le
altre immagini sono pubbliche e fissate per digest.

**Solo PostgreSQL.** Una VM su SQL Server non ha qui una procedura di backup; la produzione su SQL Server passa
per Azure SQL e i suoi backup automatici ([deployment-azure.md](deployment-azure.md)).

**Un host con `COMPOSE_FILE` nel suo `.env`** (l’host di «Deployment continuo», il cui proxy gira in un
container): ogni comando di questa sezione viene eseguito come `docker compose --project-directory <directory> …`
**senza `-f`**. Un `-f docker-compose.prod.yml` sovrascriverebbe `COMPOSE_FILE` ed escluderebbe
`docker-compose.host.yml`: Caddy tornerebbe, e i volumi e la rete dell’host andrebbero
persi. «Ripristino» fornisce i comandi completi di quell’host.

### Primo deployment

1. Una VM con Docker (Compose 2.24 o successivo), le porte 80 e 443 aperte verso Internet, e due nomi DNS
   che puntano a essa: uno per l’Api (raggiunta da GitHub, dagli agent della CI e dai proxy dei badge), uno per
   la console Web (ADR-011).
2. Copiare `deploy/vm` sulla VM, poi `cp .env.example .env` e compilare ogni valore: `IMAGE_PREFIX` e
   `TAG` (una release, senza la `v`), i nomi host, `ACME_EMAIL`, `DB_PASSWORD`,
   `DP_CERT_PASSWORD`.
3. Mettere il certificato di Data Protection (PKCS#12) in `DP_CERT_FILE` (`./secrets/dp.pfx`). Usare un certificato
   gestito; `scripts/new-dp-certificate.ps1` ne crea uno autofirmato. **Eseguirne il backup separatamente dal
   database** (un password manager, un vault): senza di esso i secret memorizzati (chiave della GitHub App, secret del webhook,
   secret OAuth, token protetti) sono illeggibili e vanno digitati di nuovo; i dati di copertura
   sopravvivono.
4. `docker compose -f docker-compose.prod.yml pull`, poi `docker compose -f docker-compose.prod.yml up -d`.
   L’Api migra lo schema all’avvio; il Web resta non pronto finché lo schema non è aggiornato.
5. Aprire `https://<web host>/setup` per creare la GitHub App dal suo manifest.
6. Installare l’App: la fine di `/setup`, l’intestazione di `/admin/organizations` e la home page vuota di
   un amministratore dell’istanza rimandano a `https://github.com/apps/<slug>/installations/new`. GitHub chiede
   quale account la installa e su quali repository, poi rimanda il browser alla console
   (`setup_url` del manifest; un’App creata prima di M4.02i non ne ha uno, impostarlo nelle sue impostazioni su
   GitHub se si desidera il ritorno). L’installazione compare come organizzazione **in attesa**.
7. Ammetterla da `/admin/organizations`, tramite il suo login: la console legge l’id dell’account da GitHub e
   mostra «login → id» prima della conferma. Il proprio account vede subito l’organizzazione, senza
   dover accedere di nuovo. Se un’installazione non compare (un webhook perso mentre l’Api era ferma),
   **Verifica le installazioni** elenca le installazioni dell’App subito invece che all’esecuzione notturna
   (mezzanotte UTC).

Le appartenenze di un utente che ha effettuato l’accesso vengono aggiornate dal worker ogni volta che vengono lette più vecchie di
`Access:CacheMinutes`, quindi le organizzazioni restano elencate per tutta la durata del cookie di sessione; oltre
`Access:HardExpiryMinutes` senza un aggiornamento riuscito (GitHub non disponibile, refresh token revocato) non concedono
nulla finché il successivo aggiornamento non riesce.

Caddy gira all’indirizzo fisso `172.30.0.10` della rete `edge` (subnet `172.30.0.0/24`), l’unico
proxy di cui l’Api e il Web si fidano per `X-Forwarded-For` e `X-Forwarded-Proto` (il Web costruisce da esso la callback
https dell’accesso GitHub). Per un Caddy installato direttamente sulla VM, usare invece `Caddyfile.example`
alla radice con il `docker-compose.yml` della radice.

### Aggiornamento

Impostare il nuovo `TAG` in `.env`, eseguire il pull, poi **avviare prima l’Api**, che migra lo schema, e il
Web dopo di essa:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d api
docker compose -f docker-compose.prod.yml up -d web
```

Una migrazione va solo in avanti: prima di un aggiornamento, eseguire un backup (più sotto, «Su richiesta»).

### Backup

Il servizio `backup` esegue `pg_dump -Fc` all’avvio e poi ogni `BACKUP_INTERVAL_HOURS` (24) nel
volume `backups`, conserva `BACKUP_RETENTION_DAYS` giorni (14), e diventa **unhealthy** quando l’ultimo
successo risale a più di 26 ore fa (`docker compose ps`). Un dump contiene **tutte le organizzazioni**
dell’istanza (ADR-013): chi legge i backup le legge tutte; limitare l’accesso allo storage di conseguenza.

**Copia fuori dalla macchina.** I dump sulla VM non sopravvivono alla perdita della VM. Impostare `BACKUP_HOOK` su un
comando shell eseguito a ogni nuovo dump, con il suo percorso in `$1`, che lo copia su uno storage esterno alla macchina (un
object store, un altro host). L’obiettivo del punto di ripristino di ARCHITECTURE §12.5, 24 ore al massimo,
vale solo con questa copia in atto.

**Su richiesta** (prima di un aggiornamento):

```bash
docker compose -f docker-compose.prod.yml run --rm -e BACKUP_ONCE=true backup
```

### Ripristino

Ogni trimestre, ripristinare il dump più recente su una VM di test e verificare che l’Api vi sia pronta. La copia
ripristinata contiene la chiave della GitHub App e i job in coda della produzione: **il suo worker resta disattivato**, il suo servizio
di backup non viene avviato e il suo `BACKUP_HOOK` è vuoto, in modo che non agisca sui repository reali
né sovrascriva le copie fuori dalla macchina della produzione:

```bash
# On the test VM, with the same deploy/vm and a fresh database volume. In its .env: WORKER_ENABLED=false
# and BACKUP_HOOK= (empty).
docker compose -f docker-compose.prod.yml up -d db
docker compose -f docker-compose.prod.yml cp ./codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose -f docker-compose.prod.yml exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
docker compose -f docker-compose.prod.yml up -d --no-deps api web   # never the backup service
```

Sull’host di «Deployment continuo» (`COMPOSE_FILE` in `.env`), per ripristinare un dump **al posto** del
database di produzione, ad esempio per tornare a una versione precedente: il database viene eliminato e ricreato,
poiché `pg_restore` lascerebbe le tabelle aggiunte da una migrazione più recente.

```bash
cd /data03/docker/code-coverage
docker compose --project-directory . stop web api
docker compose --project-directory . exec db psql -U cc -d postgres -c 'DROP DATABASE codecoverage WITH (FORCE)'
docker compose --project-directory . exec db createdb -U cc codecoverage
docker compose --project-directory . cp ./data/backups/codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose --project-directory . exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
# set TAG in .env to the version the dump belongs to, then the Api first:
docker compose --project-directory . up -d api
docker compose --project-directory . up -d web
```

La VM di test necessita dello stesso certificato di Data Protection per leggere i secret memorizzati. Una copia ripristinata esegue
il suo worker (`WORKER_ENABLED=true`) e il suo servizio di backup solo quando **sostituisce** la produzione, una volta
fermata la vecchia istanza.
`scripts/test-backup-restore.ps1` esegue la stessa esercitazione su Docker locale con uno schema di esempio (immagine
di backup, `pg_restore` in un server nuovo, conteggi delle righe confrontati); viene saltato con un motivo in assenza di
Docker.

### La riga di comando di amministrazione

La riga di comando dell’Api resta come percorso di emergenza documentato (PLAN §11-4): ammettere un’organizzazione
o creare un token di upload senza la console.

```bash
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin create-upload-token --owner my-org --scope organization --label ci
```

Ciò che fa viene registrato nel registro di audit sotto l’attore `[command-line]`.

## Deployment continuo

Ogni tag `v*` la cui CI riesce, pre-release incluse, viene distribuito sull’host di produzione dal
job `deploy` di [`ci-cd.yml`](../.github/workflows/ci-cd.yml) (M4.02b). Il job gira su un runner
self-hosted di quell’host, nell’environment GitHub `production`, e attende l’approvazione del suo reviewer.
Non gira mai su una pull request. Esegue
[`scripts/Deploy-Production.ps1`](../scripts/Deploy-Production.ps1) dal checkout del tag sul
compose di [`deploy/vm`](../deploy/vm), qui dietro il Nginx Proxy Manager (NPX) già in esecuzione
sull’host al posto di Caddy.

Cosa fa un deployment, in quest’ordine:

1. Verifica gli strumenti dell’host, poi che il commit del tag sia su `origin/main` (una versione
   stabile) oppure su `origin/develop` o su un branch `release/*` (una pre-release).
2. Effettua il login su GHCR con il token del job, in una configurazione Docker del job, mai in
   `~/.docker`.
3. Verifica `.env`, `secrets/dp.pfx` e le directory dei dati, e confronta la versione con il
   `TAG` corrente secondo la precedenza SemVer 2.0 (`1.2.0-beta.9 < 1.2.0-beta.10 < 1.2.0-rc.1 < 1.2.0`). La
   stessa versione è un nuovo deployment, e una più vecchia viene rifiutata. Verifica anche che la rete
   Docker di `PROXY_NETWORK` esista.
4. Copia un elenco esplicito di file di `deploy/vm` in `DEPLOY_DIR`, conservando i file che sostituisce
   fino all’avvio dell’Api. Non tocca mai né `.env` né `secrets/`.
5. Esegue il pull delle immagini Api e Web della versione, e le rifiuta a meno che la loro label
   `org.opencontainers.image.revision` non sia il commit del tag.
6. Esegue un backup (`BACKUP_ONCE`). Solo allora scrive il nuovo `TAG` in `.env`, mentre quello precedente
   va in `.env.previous-tag` (un nuovo deployment della stessa versione lascia quel file com’era).
7. Avvia l’Api e attende che sia healthy (300 s; una migrazione lunga resta nel frattempo `starting` o
   `unhealthy`), poi fa lo stesso per il Web.
8. Verifica `/health/ready` di entrambi gli host tramite NPX sulla porta 443 dell’host (`curl --resolve`,
   che evita il loop NAT di un server domestico). Poi confronta il
   `/api/v1/uploader/linux-x64.sha256` servito con l’asset della release.
9. Rimuove le immagini `-api` e `-web` diverse dalla versione corrente, dal `TAG` precedente e dalla
   versione di `.env.previous-tag`. Non esegue mai un prune globale: l’host non è dedicato.

Lo script esegue per impostazione predefinita un dry run, e il job passa `-Execute`. Un dry run a mano sull’host stampa
ciò che farebbe e non modifica nulla:

```bash
pwsh scripts/Deploy-Production.ps1 -Version 1.2.3 -Revision <commit of v1.2.3> -Directory /data03/docker/code-coverage
```

### Preparare l’host

**Strumenti:** PowerShell (`pwsh`) 7.4 o successivo, `curl`, Docker con il plugin compose 2.24 o successivo.
Il plugin compose deve essere installato **a livello di sistema** (il pacchetto apt `docker-compose-plugin`, in
`/usr/libexec/docker/cli-plugins` o `/usr/lib/docker/cli-plugins`), non in `~/.docker/cli-plugins`:
il job fa puntare `DOCKER_CONFIG` a una propria directory, dove un plugin per utente non viene trovato.

**Utente del runner.** L’utente del servizio del runner fa parte del gruppo `docker`, il che **equivale a
root** sull’host: chi esegue un job su questo runner controlla l’host. Da qui la label dedicata,
l’hook pre-job e, soprattutto, il ruleset dei tag descritto più sotto.

**Ciò che può girare sul runner è deciso da chi può fare il push di un tag `v*`.** L’hook accetta qualsiasi job del
`ci-cd.yml` di un tag `v*`, e quel file proviene dal tag stesso: un tag può portare un `ci-cd.yml` con
un job che punta a `cc-production` e non dichiara alcun environment. Il reviewer obbligatorio di
`production` trattiene solo il job `deploy`; non protegge il runner. Il ruleset dei tag su
`refs/tags/v*` («Impostazioni di GitHub» più sotto) è quindi **obbligatorio**, in atto prima che il runner riceva
la sua label, e va riverificato ogni volta che cambiano i collaboratori del repository.

**Directory di produzione** (`DEPLOY_DIR=/data03/docker/code-coverage`, `DATA_DIR=$DEPLOY_DIR/data`):

```bash
sudo mkdir -p /data03/docker/code-coverage/secrets /data03/docker/code-coverage/data/{db,backups,api,web}
sudo chown -R runner:runner /data03/docker/code-coverage          # the user of the runner service
sudo chown 70:70 /data03/docker/code-coverage/data/db /data03/docker/code-coverage/data/backups   # postgres alpine
sudo chown 1001:1001 /data03/docker/code-coverage/data/api /data03/docker/code-coverage/data/web  # Api and Web
sudo chmod 700 /data03/docker/code-coverage/secrets
# after copying the certificate:
sudo chown 1001:1001 /data03/docker/code-coverage/secrets/dp.pfx && sudo chmod 400 /data03/docker/code-coverage/secrets/dp.pfx
# after writing .env:
chmod 600 /data03/docker/code-coverage/.env
```

**`.env`.** Viene compilato come in «Primo deployment» più sopra, più le righe della variante NPX di
[`.env.example`](../deploy/vm/.env.example):

- `COMPOSE_FILE=docker-compose.prod.yml:docker-compose.host.yml`: compose lo legge da `.env`, quindi
  ogni comando è un semplice `docker compose --project-directory /data03/docker/code-coverage …`.
- `PROXY_NETWORK`: la rete Docker esistente di NPX.
- `PROXY_NETWORK_CIDR`: la sua subnet, ottenuta da
  `docker network inspect <network> --format '{{(index .IPAM.Config 0).Subnet}}'`.
- `DATA_DIR=/data03/docker/code-coverage/data`.

`ACME_EMAIL` lì non è usata ma deve mantenere un valore, perché compose verifica ogni variabile dei suoi
file.

[`docker-compose.host.yml`](../deploy/vm/docker-compose.host.yml) fa quattro cose:

- disattiva Caddy;
- collega l’Api e il Web alla rete NPX senza pubblicare alcuna porta;
- sostituisce `ForwardedHeaders__KnownProxies` con `ForwardedHeaders__KnownNetworks=${PROXY_NETWORK_CIDR}`:
  l’indirizzo del container NPX cambia quando viene ricreato, quindi l’Api e il Web si fidano degli
  header `X-Forwarded-*` dell’intera subnet. Collegare a quella rete solo container fidati;
- monta in bind i volumi sotto `DATA_DIR`.

`.env` e `dp.pfx` restano sull’host, mai su GitHub.

**Primo deployment, a mano.** Le immagini di un repository privato sono private:

1. Effettuare il login una volta con un personal access token classico limitato a `read:packages`
   (`docker login ghcr.io`).
2. Copiare `deploy/vm` in `DEPLOY_DIR`.
3. Eseguire `docker compose --project-directory /data03/docker/code-coverage pull`, poi `up -d`.
4. Rimuovere il token in seguito (`docker logout ghcr.io`).

I deployment successivi effettuano il login con il token del job. I volumi montati in bind devono esistere
prima del primo `up`. Un host che eseguiva già il semplice `docker-compose.prod.yml` ha volumi con nome
con gli stessi nomi ma altre opzioni, e compose li rifiuta. Eseguire un backup, rimuovere i vecchi volumi e
ripristinare nel nuovo database (vedere «Ripristino»).

### I proxy host di NPX

Aggiungere due proxy host in NPX, entrambi sulla rete indicata in `PROXY_NETWORK`:

| | Api | Web |
|---|---|---|
| Domain name | `CC_API_HOST` | `CC_WEB_HOST` |
| Scheme, forward host, port | `http`, `code-coverage-api-1`, `8080` | `http`, `code-coverage-web-1`, `8080` |
| Websockets support | non necessario | **attivo** (Blazor Server mantiene aperto un WebSocket) |
| SSL | un certificato Let’s Encrypt, Force SSL, HTTP/2 | lo stesso |

Il forward host è il nome del container di compose (progetto `code-coverage`), mai il nome del servizio
`api` o `web`: altri stack sulla rete condivisa possono usare lo stesso nome. NPX invia
`X-Forwarded-Proto` e `X-Forwarded-For` per impostazione predefinita. Il Web ne ha bisogno per costruire la callback
`https` dell’accesso GitHub, e l’Api si fida di essi solo da `PROXY_NETWORK_CIDR`. Un upload
rifiutato con 413 da NPX richiede un `client_max_body_size` più grande nella configurazione avanzata del
proxy host dell’Api.

### Il runner

- **Label dedicata.** Aggiungere la label `cc-production` al runner: il job punta a
  `[self-hosted, cc-production]`, mai a `self-hosted` da solo. Non eseguire nessun altro runner su questo host.
- **Hook pre-job.** Installare
  [`deploy/vm/runner/job-started.sh`](../deploy/vm/runner/job-started.sh) come root, fuori dalla portata
  dell’utente del runner (ad esempio `/opt/actions-runner-hooks/job-started.sh`, di proprietà di root, modalità 755). Indicarlo
  nel `.env` della directory del runner, poi riavviare il servizio del runner:

  ```bash
  ACTIONS_RUNNER_HOOK_JOB_STARTED=/opt/actions-runner-hooks/job-started.sh
  CC_DEPLOY_REPOSITORY=<owner>/code-coverage
  ```

  L’hook fa fallire qualsiasi job che non soddisfi tutte queste condizioni:
  - proviene da questo repository;
  - il suo evento è `push`;
  - il suo ref è `refs/tags/v*`;
  - il suo `GITHUB_WORKFLOW_REF` inizia con
    `<owner>/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v`.

  Una pull request che punta alla label viene quindi rifiutata prima del suo primo step. Senza
  `CC_DEPLOY_REPOSITORY`, ogni job viene rifiutato. `sh deploy/vm/runner/test-job-started.sh` lo testa
  offline, e la CI esegue quel test.

### Impostazioni di GitHub (owner)

- **Environment `production`:**
  - in deployment branches and tags, sostituire la regola di branch `v*` con una regola di **tag** `v*.*.*`;
  - aggiungere l’owner come **required reviewer**. Il job attende l’approvazione prima di partire, e
    l’attesa non conta nel suo `timeout-minutes` (20). Un’approvazione in sospeso scade dopo 30 giorni,
    e il job allora fallisce;
  - aggiungere la variabile `DEPLOY_DIR` = `/data03/docker/code-coverage`;
  - aggiungere la variabile `PRODUCTION_URL` = l’indirizzo https del Web (`https://<CC_WEB_HOST>`), il
    link che l’esecuzione mostra per l’environment. È facoltativa: se non è impostata, l’esecuzione non mostra alcun link.
- **Ruleset dei tag** su `refs/tags/v*` (**obbligatorio**, vedere «Preparare l’host»): solo l’owner può
  creare, spostare o eliminare un tag di questo tipo. È un tag a distribuire, e il suo `ci-cd.yml` è ciò che gira sul
  runner.
- **Pacchetti** `code-coverage-api` e `code-coverage-web` su GHCR: in «Manage Actions access»,
  dare a questo repository il ruolo `Read`, in modo che il token del job possa eseguirne il pull.

### Nuovo deployment, ritorno, raffiche

- **Ridistribuire** la stessa versione con «Re-run job» sull’esecuzione del tag, che mantiene il contesto del
  tag. Non esiste `workflow_dispatch`.
- **Tornare a una versione precedente.** Una migrazione va solo in avanti, quindi si tratta di un’operazione manuale
  sull’host:
  1. fermare il Web e l’Api;
  2. ripristinare il dump eseguito prima del deployment («Ripristino», i comandi di un host con
     `COMPOSE_FILE`);
  3. fare il checkout del tag precedente in una propria directory (`git clone --branch v1.2.2 …`): il suo
     `scripts/Deploy-Production.ps1` copia il `deploy/vm` del proprio checkout (`-Source` vale quello per
     impostazione predefinita), quindi i file corrispondono alla versione;
  4. effettuare il login su GHCR con un token classico limitato a `read:packages` (`docker login ghcr.io`): lo
     script esegue sempre il pull;
  5. eseguire quello script con la versione precedente, il suo commit, `-AllowDowngrade` e `-Execute`:

     ```bash
     pwsh scripts/Deploy-Production.ps1 -Version 1.2.2 -Revision <commit of v1.2.2> -Directory /data03/docker/code-coverage -AllowDowngrade -Execute
     ```

  6. effettuare il logout in seguito (`docker logout ghcr.io`).
- **Un fallimento prima dell’avvio dell’Api** rimette il `TAG` precedente e i file di `deploy/vm` che
  aveva sostituito; i container in esecuzione non sono stati toccati. Un dump eseguito dall’esecuzione fallita resta nei
  backup.
- **Un fallimento dopo l’avvio dell’Api** non viene annullato automaticamente, perché lo schema potrebbe
  essere stato migrato. Il job fallisce e indica la directory dei dati, il dump e i passi:
  1. fermare l’Api e il Web;
  2. eseguire `pg_restore` del dump;
  3. impostare `TAG` al contenuto di `.env.previous-tag` (un nuovo deployment della stessa versione lascia quel
     file com’era, quindi indica ancora la versione a cui tornare);
  4. eseguire `up -d api`, poi `up -d web`.

  Le immagini della versione corrente, del `TAG` precedente e della versione in
  `.env.previous-tag` non vengono mai rimosse dalla pulizia, in modo che il ritorno le trovi sull’host.
- **Una raffica di tag.** I deployment si accodano nel gruppo di concorrenza `production` e non vengono mai
  annullati, ma GitHub conserva solo quello in esecuzione e l’ultimo in attesa. Un tag più vecchio della
  versione distribuita viene allora rifiutato («older than the deployed»), come previsto.

### Prima di rendere pubblico il repository

È obbligatorio: un runner self-hosted di un repository pubblico può essere raggiunto dalle pull request
dei fork.

- In «Fork pull request workflows from outside collaborators», richiedere un’approvazione per **tutti** i
  collaboratori esterni.
- Verificare che il ruleset dei tag su `refs/tags/v*` sia attivo e consenta solo all’owner di creare, spostare o
  eliminare un tag. È un controllo bloccante, non un elemento di revisione: un tag decide cosa gira sul runner.
- Mantenere in atto l’hook e la label dedicata, oppure annullare la registrazione del runner.
- Rivedere la visibilità dei pacchetti GHCR.
