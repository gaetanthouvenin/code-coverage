---
source-sha256: 0524e49244f07e98e06a1af2afbad0aef581becca61efa829d47566ad6d4c7fc
---
# Operaciones

Cómo hacer funcionar una instancia de CodeCoverage en producción. El diseño que hay detrás de estos pasos está en
[ARCHITECTURE.md](ARCHITECTURE.md) §12 (en francés); esta guía es la visión del operador.

## Alertas

Los hosts exportan sus métricas solo mediante OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`, un valor de arranque;
meter `CodeCoverage`, ARCHITECTURE §12.6). [`deploy/observability`](../deploy/observability) las convierte
en alertas de Prometheus:

- `alerts.yml`: las reglas, con sus umbrales (nunca en la configuración de los hosts);
- `alerts.test.yml`: sus pruebas unitarias, ejecutadas por la CI con `promtool test rules`;
- `docker-compose.observability.yml`: una superposición que añade un OpenTelemetry Collector (OTLP de entrada,
  exportador Prometheus de salida) y Prometheus, ambos fijados por digest:

  ```bash
  docker compose -f docker-compose.yml -f deploy/observability/docker-compose.observability.yml up -d
  ```

  Las alertas activas se enumeran en <http://localhost:9090/alerts>.

La superposición no envía ninguna notificación. Conecte Prometheus al receptor que elija (Alertmanager con
correo o chat, un Prometheus alojado) fuera de este repositorio: las URL de los receptores y las contraseñas son secretos
y nunca van en estos archivos.

Las métricas no se separan por organización (ADR-013): el stack de supervisión ve los nombres de repositorio de
todas las organizaciones (`uploads_received_total{repo}`) y debe restringirse a los operadores de la
instancia, como las copias de seguridad de la base de datos.

### Las alertas

| Alerta | Expresión | Significado | Primer diagnóstico |
|---|---|---|---|
| `CodeCoverageDeadJobs` | `max(jobs_dead) > 0` durante 1 min | Un job agotó todos sus intentos: la notificación, el análisis o la importación que llevaba nunca se produjo. | `/admin/operations`, lea el último error del job muerto, corrija la causa y luego reinténtelo (`POST /api/v1/ops/jobs/{id}/retry`). Los jobs muertos permanecen hasta que se reintentan o se purgan tras `Retention:JobDays`. |
| `CodeCoverageJobQueueStalled` | `max(job_oldest_queued_age_seconds) > 600` durante 2 min | El job ejecutable más antiguo lleva esperando más de 10 minutos. | ¿Hay una Api con `Worker:Enabled` en ejecución? Sus registros, la carga de la base de datos. |
| `CodeCoverageWorkerHeartbeatStale` | `max(worker_heartbeat_age_seconds) > 120` durante 1 min | Ningún worker ha emitido latido desde hace 2 minutos (un worker late cada 15 s y escribe `WorkerHeartbeats`). | Inicie una Api con el worker, o averigüe por qué se detuvo. |
| `CodeCoverageWorkerHeartbeatMissing` | ningún `worker_heartbeat_age_seconds` mientras se exportan los indicadores de los jobs, durante 5 min | Ningún worker ha latido nunca, o su último latido se purgó (de más de un día), situación en la que la regla stale se resuelve. | Inicie una Api con el worker, o averigüe por qué se detuvo. |
| `CodeCoverageMetricsAbsent` | `absent_over_time(job_oldest_queued_age_seconds[5m])` | Ninguna Api ha exportado los indicadores de los jobs durante 5 minutos. | Todas las Api caídas, o el endpoint OTLP, el collector o Prometheus averiados. |
| `CodeCoverageWebhookSignatureFailures` | `sum(increase(webhook_signature_failures_total[10m])) > 0`, o una serie ausente hace 10 min que ahora es mayor que 0 | Entregas de webhook con una firma no válida. | `GitHub:WebhookSecret` desincronizado con la App tras una rotación, o solicitudes falsificadas. |
| `CodeCoverageGitHubQuotaLow` | restante registrado / límite `< 0.10` por instalación, durante 5 min | Queda menos del 10 % de la cuota de GitHub de una instalación. | Los jobs que llaman a GitHub esperan al restablecimiento sin perder un intento; busque una ráfaga (reprocesamiento, barrido). |

### Nombres de las métricas

El collector exporta los nombres del meter tal cual (`add_metric_suffixes: false`): los nombres
ya llevan su unidad (`_seconds`) y `_total`. Un indicador leído de la base de datos (`jobs_dead`,
`worker_heartbeat_age_seconds`, `jobs_queued`, `job_oldest_queued_age_seconds`) lo muestrea cada 30 s
cada Api, solo mientras un listener recopila los indicadores de los jobs. Lo mismo ocurre con `github_ratelimit_limit` y
`github_ratelimit_recorded_remaining` (etiqueta `installation`): ambos proceden de las mismas filas de `GitHubQuotas`,
y una cuota cuya hora de restablecimiento ha pasado cuenta como completa. `github_ratelimit_remaining` informa de la
última respuesta que leyó este proceso; la alerta no lo usa. Los contadores se reinician cuando un host se reinicia: las reglas usan `increase()`.

### Comprobar las reglas

```bash
docker run --rm --entrypoint promtool -v "$PWD/deploy/observability:/rules:ro" -w /rules prom/prometheus:v3.5.0 test rules alerts.test.yml
```

### Provocar las alertas en un stack local

Con la superposición en ejecución (y `OTEL_METRIC_EXPORT_INTERVAL` a 10 s, como la define la superposición):

| Alerta | Cómo |
|---|---|
| `CodeCoverageDeadJobs` | Inserte una fila de `Jobs` en el estado `Dead`, o deje que un job falle su último intento. |
| `CodeCoverageWebhookSignatureFailures` | `POST /api/v1/github/webhooks` con un `X-Hub-Signature-256` incorrecto (401). |
| `CodeCoverageWorkerHeartbeatStale` | Reinicie la Api con `Worker__Enabled=false`. |
| `CodeCoverageWorkerHeartbeatMissing` | Inicie una instancia nueva con `Worker__Enabled=false` (nunca se escribe ningún latido). |
| `CodeCoverageJobQueueStalled` | Sin worker, ponga en cola un job cuyo `RunAfter` tenga más de 10 minutos de antigüedad. |
| `CodeCoverageMetricsAbsent` | Detenga la Api y la Web (el collector conserva una serie 5 minutos y luego la regla espera 5 más). |
| `CodeCoverageGitHubQuotaLow` | Requiere respuestas de GitHub con un `x-ratelimit-remaining` bajo; cubierto por `alerts.test.yml`. |

## Producción en una VM

[`deploy/vm`](../deploy/vm) ejecuta toda la instancia en un único host Docker (M4.02): Caddy (TLS, ACME),
PostgreSQL 17, la Api (API REST, worker de jobs, migraciones), la Web (consola) y un contenedor de copias de seguridad.
Solo imágenes: la Api y la Web proceden del registro de la CI en la versión de una release, las
demás imágenes son públicas y están fijadas por digest.

**Solo PostgreSQL.** Una VM con SQL Server no tiene aquí ninguna receta de copia de seguridad; la producción con SQL Server pasa
por Azure SQL y sus copias de seguridad automáticas ([deployment-azure.md](deployment-azure.md)).

**Un host con `COMPOSE_FILE` en su `.env`** (el host de «Despliegue continuo», cuyo proxy se ejecuta en un
contenedor): todos los comandos de esta sección se ejecutan como `docker compose --project-directory <directory> …`
**sin `-f`**, desde ese directorio (`cd <directory>` primero: un `COMPOSE_FILE` relativo se resuelve desde el
directorio actual). Un `-f docker-compose.prod.yml` anularía `COMPOSE_FILE` y descartaría
`docker-compose.host.yml`: Caddy volvería, y se perderían los volúmenes y la red del host.
«Restauración» da los comandos completos de ese host.

### Primer despliegue

1. Una VM con Docker (Compose 2.24 o posterior), los puertos 80 y 443 abiertos a Internet, y dos nombres DNS
   que apunten a ella: uno para la Api (al que acceden GitHub, los agentes de CI y los proxies de badges), otro para
   la consola Web (ADR-011).
2. Copie `deploy/vm` a la VM, luego `cp .env.example .env` y rellene todos los valores: `IMAGE_PREFIX` y
   `TAG` (una release, sin la `v`), los nombres de host, `ACME_EMAIL`, `DB_PASSWORD`,
   `DP_CERT_PASSWORD`.
3. Coloque el certificado de Data Protection (PKCS#12) en `DP_CERT_FILE` (`./secrets/dp.pfx`). Use un certificado
   administrado; `scripts/new-dp-certificate.ps1` genera uno autofirmado. **Haga una copia de seguridad aparte de la
   base de datos** (un gestor de contraseñas, un almacén de secretos): sin él, los secretos almacenados (clave de la GitHub App, secreto del
   webhook, secreto OAuth, tokens protegidos) son ilegibles y deben volver a introducirse; los datos de cobertura
   se conservan.
4. `docker compose -f docker-compose.prod.yml pull`, luego `docker compose -f docker-compose.prod.yml up -d`.
   La Api migra el esquema al iniciarse; la Web permanece no lista hasta que el esquema está al día.
5. Abra `https://<web host>/setup` para crear la GitHub App a partir de su manifiesto.
6. Instale la App: el final de `/setup`, el encabezado de `/admin/organizations` y la página de inicio vacía de
   un administrador de la instancia enlazan a `https://github.com/apps/<slug>/installations/new`. GitHub pregunta
   qué cuenta la instala y en qué repositorios, y luego devuelve el navegador a la consola
   (`setup_url` del manifiesto; una App creada antes de M4.02i no lo tiene, defínalo en su configuración en
   GitHub si quiere el retorno). La instalación aparece como una organización **pendiente**.
7. Admítala desde `/admin/organizations`, por su login: la consola lee el id de la cuenta en GitHub y
   muestra «login → id» antes de que confirme. Su propia cuenta ve la organización de inmediato, sin
   volver a iniciar sesión. Si una instalación no aparece (un webhook perdido mientras la Api estaba caída),
   **Comprobar las instalaciones** enumera las instalaciones de la App en ese momento en lugar de en la ejecución nocturna
   (medianoche UTC).

El worker actualiza las pertenencias de un usuario con sesión iniciada cada vez que se leen con una antigüedad superior a
`Access:CacheMinutes`, de modo que las organizaciones siguen apareciendo durante toda la vida de la cookie de sesión; pasado
`Access:HardExpiryMinutes` sin una actualización correcta (GitHub caído, refresh token revocado) no conceden
nada hasta que la siguiente actualización tenga éxito.

Caddy se ejecuta en la dirección fija `172.30.0.10` de la red `edge` (subred `172.30.0.0/24`), el único
proxy en el que la Api y la Web confían para `X-Forwarded-For` y `X-Forwarded-Proto` (la Web construye a partir de él el callback https
del inicio de sesión con GitHub). Para un Caddy instalado en la propia VM, use en su lugar `Caddyfile.example`
de la raíz con el `docker-compose.yml` de la raíz.

### Actualización

Defina el nuevo `TAG` en `.env`, descargue las imágenes y luego **inicie primero la Api**, que migra el esquema, y la
Web después:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d api
docker compose -f docker-compose.prod.yml up -d web
```

Una migración solo va hacia adelante: antes de una actualización, haga una copia de seguridad (más abajo, «A demanda»).

### Copias de seguridad

El servicio `backup` ejecuta `pg_dump -Fc` al iniciarse y luego cada `BACKUP_INTERVAL_HOURS` (24) en el
volumen `backups`, conserva `BACKUP_RETENTION_DAYS` días (14) y pasa a **unhealthy** cuando el último
éxito tiene más de 26 horas (`docker compose ps`). Un volcado contiene **todas las organizaciones** de la
instancia (ADR-013): quien lee las copias de seguridad las lee todas; restrinja el almacenamiento en consecuencia.

**Copia fuera de la máquina.** Los volcados de la VM no sobreviven a la pérdida de la VM. Defina `BACKUP_HOOK` con un
comando de shell ejecutado en cada nuevo volcado, con su ruta en `$1`, que lo copie a un almacenamiento fuera de la máquina (un
almacén de objetos, otro host). El objetivo de punto de recuperación de ARCHITECTURE §12.5, 24 horas como máximo,
solo se cumple con esta copia en marcha.

**A demanda** (antes de una actualización):

```bash
docker compose -f docker-compose.prod.yml run --rm -e BACKUP_ONCE=true backup
```

### Restauración

Cada trimestre, restaure el último volcado en una VM de prueba y compruebe que la Api está lista en ella. La copia
restaurada contiene la clave de la GitHub App y los jobs en cola de producción: **su worker permanece desactivado**, su servicio
de copias de seguridad no se inicia y su `BACKUP_HOOK` está vacío, para que no actúe sobre los repositorios reales
ni sobrescriba las copias fuera de la máquina de producción:

```bash
# On the test VM, with the same deploy/vm and a fresh database volume. In its .env: WORKER_ENABLED=false
# and BACKUP_HOOK= (empty).
docker compose -f docker-compose.prod.yml up -d db
docker compose -f docker-compose.prod.yml cp ./codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose -f docker-compose.prod.yml exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
docker compose -f docker-compose.prod.yml up -d --no-deps api web   # never the backup service
```

En el host de «Despliegue continuo» (`COMPOSE_FILE` en `.env`), restaurar un volcado **en lugar** de
la base de datos de producción, por ejemplo para volver a una versión anterior: la base de datos se elimina y se vuelve a
crear, ya que `pg_restore` dejaría las tablas que añadió una migración más reciente.

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

La VM de prueba necesita el mismo certificado de Data Protection para leer los secretos almacenados. Una copia restaurada ejecuta
su worker (`WORKER_ENABLED=true`) y su servicio de copias de seguridad solo cuando **sustituye** a la producción, una vez
detenida la instancia anterior.
`scripts/test-backup-restore.ps1` ejecuta el mismo simulacro en Docker local con un esquema de ejemplo (imagen
de copia de seguridad, `pg_restore` en un servidor nuevo, recuentos de filas comparados); se omite con un motivo si no hay
Docker.

### La línea de comandos de administración

La línea de comandos de la Api sigue siendo la vía de emergencia documentada (PLAN §11-4): admitir una
organización o crear un token de upload sin la consola.

```bash
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin create-upload-token --owner my-org --scope organization --label ci
```

Lo que hace se audita con el actor `[command-line]`.

## Despliegue continuo

Todo tag `v*` cuya CI tiene éxito, prereleases incluidas, se despliega en el host de producción mediante el
job `deploy` de [`ci-cd.yml`](../.github/workflows/ci-cd.yml) (M4.02b). El job se ejecuta en un runner
autoalojado de ese host, en el entorno de GitHub `production`, y espera a que su revisor lo apruebe.
Nunca se ejecuta en una pull request. Ejecuta
[`scripts/Deploy-Production.ps1`](../scripts/Deploy-Production.ps1) desde el checkout del tag sobre
el compose de [`deploy/vm`](../deploy/vm), aquí detrás del Nginx Proxy Manager (NPX) que ya se ejecuta
en el host en lugar de Caddy.

Lo que hace un despliegue, en este orden:

1. Comprueba las herramientas del host y luego que el commit del tag está en `origin/main` (una versión
   estable) o en `origin/develop` o en una rama `release/*` (una prerelease).
2. Inicia sesión en GHCR con el token del job, en una configuración de Docker propia del job, nunca en
   `~/.docker`.
3. Comprueba `.env`, `secrets/dp.pfx` y los directorios de datos, y compara la versión con el
   `TAG` actual según la precedencia de SemVer 2.0 (`1.2.0-beta.9 < 1.2.0-beta.10 < 1.2.0-rc.1 < 1.2.0`). La
   misma versión es un redespliegue, y una anterior se rechaza. También comprueba que existe la red de
   Docker de `PROXY_NETWORK`.
4. Copia una lista explícita de archivos de `deploy/vm` en `DEPLOY_DIR`, conservando los archivos que sustituye
   hasta que la Api se inicia. Nunca toca `.env` ni `secrets/`.
5. Descarga las imágenes de la Api y de la Web de la versión, y las rechaza salvo que su etiqueta
   `org.opencontainers.image.revision` sea el commit del tag.
6. Hace una copia de seguridad (`BACKUP_ONCE`). Solo entonces escribe el nuevo `TAG` en `.env`, y el anterior
   pasa a `.env.previous-tag` (un redespliegue deja ese archivo como estaba).
7. Inicia la Api y espera a que esté sana (300 s; una migración larga permanece `starting` o
   `unhealthy` mientras tanto), y luego hace lo mismo con la Web.
8. Comprueba `/health/ready` de ambos hosts a través de NPX en el puerto 443 del host (`curl --resolve`,
   que evita el bucle NAT de un servidor doméstico). Después compara el
   `/api/v1/uploader/linux-x64.sha256` servido con el asset de la release.
9. Elimina las imágenes `-api` y `-web` distintas de la versión actual, del `TAG` anterior y de la
   versión de `.env.previous-tag`. Nunca ejecuta un prune global: el host no está dedicado.

El script funciona de forma predeterminada en modo simulación, y el job pasa `-Execute`. Una simulación a mano en el host muestra
lo que haría y no cambia nada:

```bash
pwsh scripts/Deploy-Production.ps1 -Version 1.2.3 -Revision <commit of v1.2.3> -Directory /data03/docker/code-coverage
```

### Preparar el host

**Herramientas:** PowerShell (`pwsh`) 7.4 o posterior, `curl`, Docker con el plugin compose 2.24 o posterior.
El plugin compose debe instalarse **para todo el sistema** (el paquete apt `docker-compose-plugin`, en
`/usr/libexec/docker/cli-plugins` o `/usr/lib/docker/cli-plugins`), no en `~/.docker/cli-plugins`:
el job apunta `DOCKER_CONFIG` a un directorio propio, donde no se encuentra un plugin por usuario.

**Usuario del runner.** El usuario del servicio del runner pertenece al grupo `docker`, lo que **equivale a
root** en el host: quien ejecuta un job en este runner controla el host. De ahí la etiqueta dedicada,
el hook previo al job y, sobre todo, el ruleset de tags que se describe más abajo.

**Lo que puede ejecutarse en el runner lo decide quién puede hacer push de un tag `v*`.** El hook acepta cualquier job del
`ci-cd.yml` de un tag `v*`, y ese archivo procede del propio tag: un tag puede llevar un `ci-cd.yml` con
un job que apunte a `cc-production` y no declare ningún entorno. El revisor obligatorio de
`production` solo retiene el job `deploy`; no protege el runner. Por tanto, el ruleset de tags sobre
`refs/tags/v*` («Configuración de GitHub» más abajo) es **obligatorio**, debe estar en vigor antes de que el runner reciba
su etiqueta, y debe volver a comprobarse cada vez que cambian los colaboradores del repositorio.

**Directorio de producción** (`DEPLOY_DIR=/data03/docker/code-coverage`, `DATA_DIR=$DEPLOY_DIR/data`):

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

**`.env`.** Se rellena como en «Primer despliegue» más arriba, más las líneas de la variante NPX de
[`.env.example`](../deploy/vm/.env.example):

- `COMPOSE_FILE=docker-compose.prod.yml:docker-compose.host.yml`: compose lo lee de `.env`, de modo que
  todo comando es un simple `docker compose --project-directory /data03/docker/code-coverage …`, ejecutado
  **desde ese directorio** (`cd /data03/docker/code-coverage` primero). Compose resuelve una ruta relativa de
  `COMPOSE_FILE` desde su directorio actual, no desde `--project-directory`: ejecutado desde otro lugar, falla con
  `stat <directorio actual>/docker-compose.prod.yml: no such file or directory`. La forma absoluta
  (`/data03/docker/code-coverage/docker-compose.prod.yml:/data03/docker/code-coverage/docker-compose.host.yml`)
  funciona desde cualquier lugar. El script de despliegue acepta ambas: ejecuta cada comando compose desde
  `DEPLOY_DIR` y vuelve después a su propio directorio, incluso tras un fallo (M4.02c).
- `PROXY_NETWORK`: la red de Docker existente de NPX.
- `PROXY_NETWORK_CIDR`: su subred, obtenida con
  `docker network inspect <network> --format '{{(index .IPAM.Config 0).Subnet}}'`.
- `DATA_DIR=/data03/docker/code-coverage/data`.

`ACME_EMAIL` no se usa ahí, pero debe conservar un valor, porque compose comprueba todas las variables de sus
archivos.

[`docker-compose.host.yml`](../deploy/vm/docker-compose.host.yml) hace cuatro cosas:

- desactiva Caddy;
- conecta la Api y la Web a la red de NPX sin publicar ningún puerto;
- sustituye `ForwardedHeaders__KnownProxies` por `ForwardedHeaders__KnownNetworks=${PROXY_NETWORK_CIDR}`:
  la dirección del contenedor de NPX cambia cuando se recrea, por lo que la Api y la Web confían en las
  cabeceras `X-Forwarded-*` de toda la subred. Conecte a esa red solo contenedores de confianza;
- monta mediante bind mount los volúmenes bajo `DATA_DIR`.

`.env` y `dp.pfx` permanecen en el host, nunca en GitHub.

**Primer despliegue, a mano.** Las imágenes de un repositorio privado son privadas:

1. Inicie sesión una vez con un personal access token clásico limitado a `read:packages`
   (`docker login ghcr.io`).
2. Copie `deploy/vm` a `DEPLOY_DIR`.
3. Ejecute `cd /data03/docker/code-coverage`, luego `docker compose --project-directory . pull` y luego
   `docker compose --project-directory . up -d`.
4. Elimine el token después (`docker logout ghcr.io`).

Los despliegues siguientes inician sesión con el token del job. Los volúmenes montados mediante bind mount deben existir
antes del primer `up`. Un host que ya ejecutó el `docker-compose.prod.yml` simple tiene volúmenes con nombre
con los mismos nombres y otras opciones, y compose los rechaza. Haga una copia de seguridad, elimine los volúmenes antiguos y
restaure en la nueva base de datos (consulte «Restauración»).

### Los proxy hosts de NPX

Añada dos proxy hosts en NPX, ambos en la red indicada en `PROXY_NETWORK`:

| | Api | Web |
|---|---|---|
| Nombre de dominio | `CC_API_HOST` | `CC_WEB_HOST` |
| Esquema, host de reenvío, puerto | `http`, `code-coverage-api-1`, `8080` | `http`, `code-coverage-web-1`, `8080` |
| Compatibilidad con Websockets | no necesaria | **activada** (Blazor Server mantiene un WebSocket abierto) |
| SSL | un certificado Let's Encrypt, Force SSL, HTTP/2 | lo mismo |

El host de reenvío es el nombre del contenedor de compose (proyecto `code-coverage`), nunca el nombre del servicio
`api` o `web`: otros stacks de la red compartida pueden usar el mismo nombre. NPX envía
`X-Forwarded-Proto` y `X-Forwarded-For` de forma predeterminada. La Web los necesita para construir el callback `https`
del inicio de sesión con GitHub, y la Api solo confía en ellos desde `PROXY_NETWORK_CIDR`. Un upload
rechazado con 413 por NPX necesita un `client_max_body_size` mayor en la configuración avanzada del
host de la Api.

### El runner

- **Etiqueta dedicada.** Añada la etiqueta `cc-production` al runner: el job apunta a
  `[self-hosted, cc-production]`, nunca a `self-hosted` solo. No ejecute ningún otro runner en este host.
- **Hook previo al job.** Instale
  [`deploy/vm/runner/job-started.sh`](../deploy/vm/runner/job-started.sh) como root, fuera del alcance
  del usuario del runner (por ejemplo `/opt/actions-runner-hooks/job-started.sh`, propiedad de root, modo 755). Indíquelo
  en el `.env` del directorio del runner y luego reinicie el servicio del runner:

  ```bash
  ACTIONS_RUNNER_HOOK_JOB_STARTED=/opt/actions-runner-hooks/job-started.sh
  CC_DEPLOY_REPOSITORY=<owner>/code-coverage
  ```

  El hook hace fallar todo job que no cumpla todas estas condiciones:
  - procede de este repositorio;
  - su evento es `push`;
  - su ref es `refs/tags/v*`;
  - su `GITHUB_WORKFLOW_REF` empieza por
    `<owner>/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v`.

  Por tanto, una pull request que apunte a la etiqueta se rechaza antes de su primer paso. Sin
  `CC_DEPLOY_REPOSITORY`, se rechaza todo job. `sh deploy/vm/runner/test-job-started.sh` lo prueba
  sin conexión, y la CI ejecuta esa prueba.

### Herramientas de actualización de imágenes del host

Solo un tag `v*` desplegado por el job cambia una imagen de CodeCoverage. Cada imagen de
[`docker-compose.prod.yml`](../deploy/vm/docker-compose.prod.yml) está fijada, por digest o por el `TAG`
de una release, y cada servicio lleva dos etiquetas (M4.02d):

- `wud.watch: "false"`: WUD (What’s Up Docker) no vigila el contenedor;
- `com.centurylinklabs.watchtower.enable: "false"`: Watchtower no lo actualiza.

No cambian nada en un host sin estas herramientas. Sin ellas, una herramienta así recrea por sí misma un
contenedor sobre una imagen más reciente. El 2026-11-14, WUD recreó el contenedor de la base de datos en
PostgreSQL 18 un minuto después de que PostgreSQL 17 la inicializara. La nueva versión mayor rechazó los
datos, y la Api ya no alcanzaba `db`.

Una etiqueta se aplica cuando compose crea el contenedor. Los contenedores creados antes de las etiquetas
las reciben en su próxima recreación. El despliegue recrea la Api y la Web, y la base de datos mediante
`up -d api` cuando su configuración cambió. El servicio de copias de seguridad solo se recrea cuando
`backup/` cambia. **Antes de volver a iniciar WUD o Watchtower** en el host:

1. Recree lo que aún no tiene las etiquetas, desde el directorio de producción (los contenedores cuya
   configuración cambió se reinician, incluida la base de datos: una breve interrupción):

   ```bash
   cd /data03/docker/code-coverage
   docker compose --project-directory . up -d
   ```

2. Compruebe que cada contenedor del proyecto lleva las dos etiquetas:

   ```bash
   docker ps --filter label=com.docker.compose.project=code-coverage \
     --format '{{.Names}} wud={{.Label "wud.watch"}} watchtower={{.Label "com.centurylinklabs.watchtower.enable"}}'
   ```

   Cada línea debe terminar en `wud=false watchtower=false`.

3. Inicie la herramienta y compruebe que no recrea ningún contenedor de CodeCoverage: `docker ps` conserva
   sus fechas de creación, y WUD no los lista como vigilados.

### Configuración de GitHub (propietario)

- **Entorno `production`:**
  - en las ramas y tags de despliegue, sustituya la regla de rama `v*` por una regla de **tag** `v*.*.*`;
  - añada al propietario como **revisor obligatorio**. El job espera la aprobación antes de empezar, y la
    espera no cuenta en su `timeout-minutes` (20). Una aprobación pendiente caduca a los 30 días,
    y el job falla entonces;
  - añada la variable `DEPLOY_DIR` = `/data03/docker/code-coverage`;
  - añada la variable `PRODUCTION_URL` = la dirección https de la Web (`https://<CC_WEB_HOST>`), el
    enlace que la ejecución muestra para el entorno. Es opcional: si no se define, la ejecución no muestra ningún enlace.
- **Ruleset de tags** sobre `refs/tags/v*` (**obligatorio**, consulte «Preparar el host»): solo el propietario puede
  crear, mover o eliminar un tag así. Un tag es lo que despliega, y su `ci-cd.yml` es lo que se ejecuta en el
  runner.
- **Paquetes** `code-coverage-api` y `code-coverage-web` en GHCR: en «Manage Actions access»,
  conceda a este repositorio el rol `Read`, para que el token del job pueda descargarlos.

### Redesplegar, volver atrás, ráfagas

- **Redesplegar** la misma versión con «Re-run job» en la ejecución del tag, que conserva el contexto del
  tag. No hay `workflow_dispatch`.
- **Volver a una versión anterior.** Una migración solo va hacia adelante, por lo que es una operación manual en el
  host:
  1. detenga la Web y la Api;
  2. restaure el volcado realizado antes del despliegue («Restauración», los comandos de un host con
     `COMPOSE_FILE`);
  3. obtenga el tag anterior en un directorio propio (`git clone --branch v1.2.2 …`): su
     `scripts/Deploy-Production.ps1` copia el `deploy/vm` de su propio checkout (`-Source` lo toma
     como valor predeterminado), de modo que los archivos coinciden con la versión;
  4. inicie sesión en GHCR con un token clásico limitado a `read:packages` (`docker login ghcr.io`): el
     script siempre descarga las imágenes;
  5. ejecute ese script con la versión anterior, su commit, `-AllowDowngrade` y `-Execute`:

     ```bash
     pwsh scripts/Deploy-Production.ps1 -Version 1.2.2 -Revision <commit of v1.2.2> -Directory /data03/docker/code-coverage -AllowDowngrade -Execute
     ```

  6. cierre la sesión después (`docker logout ghcr.io`).
- **Un fallo antes del inicio de la Api** restablece el `TAG` anterior y los archivos de `deploy/vm` que
  había sustituido; los contenedores en ejecución no se tocaron. Un volcado realizado por la ejecución fallida permanece en las
  copias de seguridad.
- **Un fallo después del inicio de la Api** no se revierte automáticamente, porque el esquema puede
  haberse migrado. El job falla e indica el directorio de datos, el volcado y los pasos, cuyos comandos compose
  empiezan por `cd <DEPLOY_DIR>` (un `COMPOSE_FILE` relativo):
  1. detenga la Api y la Web;
  2. ejecute `pg_restore` del volcado;
  3. establezca `TAG` en el contenido de `.env.previous-tag` (un redespliegue de la misma versión deja ese
     archivo como estaba, por lo que sigue indicando la versión a la que volver);
  4. ejecute `up -d api` y luego `up -d web`.

  La limpieza nunca elimina las imágenes de la versión actual, del `TAG` anterior ni de la versión de
  `.env.previous-tag`, para que la vuelta atrás las encuentre en el host.
- **Una ráfaga de tags.** Los despliegues se ponen en cola en el grupo de concurrencia `production` y nunca se
  cancelan, pero GitHub solo conserva el que está en ejecución y el último pendiente. Un tag más antiguo que la
  versión desplegada se rechaza entonces («older than the deployed»), como se espera.

### Antes de hacer público el repositorio

Esto es obligatorio: las pull requests de forks pueden llegar a un runner autoalojado de un repositorio
público.

- En «Fork pull request workflows from outside collaborators», exija una aprobación para **todos** los
  colaboradores externos.
- Compruebe que el ruleset de tags sobre `refs/tags/v*` está activo y solo permite al propietario crear, mover o
  eliminar un tag. Es una comprobación bloqueante, no un elemento de revisión: un tag decide lo que se ejecuta en el runner.
- Mantenga en su lugar el hook y la etiqueta dedicada, o anule el registro del runner.
- Revise la visibilidad de los paquetes de GHCR.
