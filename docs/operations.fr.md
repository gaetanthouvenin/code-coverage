---
source-sha256: f18905149121c14cff49d26076c869c43b7e1b6aa3b489fe6578740d426f5919
---
# Exploitation

Comment faire tourner une instance CodeCoverage en production. La conception qui sous-tend ces étapes se trouve dans
[ARCHITECTURE.md](ARCHITECTURE.md) §12 (en français) ; ce guide en donne la vue de l’exploitant.

## Alertes

Les hôtes exportent leurs métriques uniquement par OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`, une valeur d’amorçage ;
meter `CodeCoverage`, ARCHITECTURE §12.6). [`deploy/observability`](../deploy/observability) les transforme
en alertes Prometheus :

- `alerts.yml` : les règles, avec leurs seuils (jamais dans la configuration des hôtes) ;
- `alerts.test.yml` : leurs tests unitaires, exécutés par la CI avec `promtool test rules` ;
- `docker-compose.observability.yml` : une surcouche qui ajoute un OpenTelemetry Collector (OTLP en entrée,
  exporter Prometheus en sortie) et Prometheus, tous deux épinglés par digest :

  ```bash
  docker compose -f docker-compose.yml -f deploy/observability/docker-compose.observability.yml up -d
  ```

  Les alertes déclenchées sont listées sur <http://localhost:9090/alerts>.

La surcouche n’envoie aucune notification. Branchez Prometheus sur le destinataire de votre choix (Alertmanager avec
mail ou messagerie, un Prometheus hébergé) en dehors de ce dépôt : les URL et mots de passe des destinataires sont des secrets
et n’entrent jamais dans ces fichiers.

Les métriques ne sont pas séparées par organisation (ADR-013) : la pile de supervision voit les noms des dépôts de
toutes les organisations (`uploads_received_total{repo}`) et doit être réservée aux exploitants de
l’instance, comme les sauvegardes de la base de données.

### Les alertes

| Alerte | Expression | Signification | Premier diagnostic |
|---|---|---|---|
| `CodeCoverageDeadJobs` | `max(jobs_dead) > 0` pendant 1 min | Un job a épuisé toutes ses tentatives : la notification, l’analyse ou l’import qu’il portait n’a jamais eu lieu. | `/admin/operations` : lisez la dernière erreur du job mort, corrigez la cause, puis relancez-le (`POST /api/v1/ops/jobs/{id}/retry`). Les jobs morts restent jusqu’à leur relance ou jusqu’à leur purge après `Retention:JobDays`. |
| `CodeCoverageJobQueueStalled` | `max(job_oldest_queued_age_seconds) > 600` pendant 2 min | Le plus ancien job exécutable attend depuis plus de 10 minutes. | Une Api avec `Worker:Enabled` tourne-t-elle ? Ses journaux, la charge de la base de données. |
| `CodeCoverageWorkerHeartbeatStale` | `max(worker_heartbeat_age_seconds) > 120` pendant 1 min | Aucun worker n’a battu depuis 2 minutes (un worker bat toutes les 15 s et écrit `WorkerHeartbeats`). | Démarrez une Api avec le worker, ou cherchez pourquoi il s’est arrêté. |
| `CodeCoverageWorkerHeartbeatMissing` | aucun `worker_heartbeat_age_seconds` alors que les jauges des jobs sont exportées, pendant 5 min | Aucun worker n’a jamais battu, ou son dernier battement a été purgé (plus d’un jour), cas où la règle « stale » se résout. | Démarrez une Api avec le worker, ou cherchez pourquoi il s’est arrêté. |
| `CodeCoverageMetricsAbsent` | `absent_over_time(job_oldest_queued_age_seconds[5m])` | Aucune Api n’a exporté les jauges des jobs depuis 5 minutes. | Toutes les Api sont arrêtées, ou le point de terminaison OTLP, le collector ou Prometheus est en panne. |
| `CodeCoverageWebhookSignatureFailures` | `sum(increase(webhook_signature_failures_total[10m])) > 0`, ou une série absente il y a 10 min et désormais au-dessus de 0 | Des livraisons de webhook avec une signature invalide. | `GitHub:WebhookSecret` désynchronisé de l’App après une rotation, ou des requêtes falsifiées. |
| `CodeCoverageGitHubQuotaLow` | restant enregistré / limite `< 0.10` par installation, pendant 5 min | Il reste moins de 10 % du quota GitHub d’une installation. | Les jobs qui appellent GitHub attendent la réinitialisation sans perdre de tentative ; cherchez un pic (retraitement, balayage). |

### Noms des métriques

Le collector exporte les noms du meter tels quels (`add_metric_suffixes: false`) : les noms
portent déjà leur unité (`_seconds`) et `_total`. Une jauge lue dans la base de données (`jobs_dead`,
`worker_heartbeat_age_seconds`, `jobs_queued`, `job_oldest_queued_age_seconds`) est échantillonnée toutes les 30 s
par chaque Api, uniquement tant qu’un listener collecte les jauges des jobs. Il en va de même pour `github_ratelimit_limit` et
`github_ratelimit_recorded_remaining` (label `installation`) : toutes deux proviennent des mêmes lignes de `GitHubQuotas`,
et un quota dont l’heure de réinitialisation est passée compte comme plein. `github_ratelimit_remaining` rapporte la
dernière réponse lue par ce processus ; l’alerte ne l’utilise pas. Les compteurs repartent de zéro au redémarrage d’un hôte : les règles utilisent `increase()`.

### Vérifier les règles

```bash
docker run --rm --entrypoint promtool -v "$PWD/deploy/observability:/rules:ro" -w /rules prom/prometheus:v3.5.0 test rules alerts.test.yml
```

### Provoquer les alertes sur une pile locale

Avec la surcouche en marche (et `OTEL_METRIC_EXPORT_INTERVAL` à 10 s, comme la surcouche le règle) :

| Alerte | Comment |
|---|---|
| `CodeCoverageDeadJobs` | Insérez une ligne de `Jobs` dans l’état `Dead`, ou laissez un job échouer à sa dernière tentative. |
| `CodeCoverageWebhookSignatureFailures` | `POST /api/v1/github/webhooks` avec un `X-Hub-Signature-256` erroné (401). |
| `CodeCoverageWorkerHeartbeatStale` | Redémarrez l’Api avec `Worker__Enabled=false`. |
| `CodeCoverageWorkerHeartbeatMissing` | Démarrez une instance neuve avec `Worker__Enabled=false` (aucun battement n’est jamais écrit). |
| `CodeCoverageJobQueueStalled` | Sans worker, mettez en file un job dont le `RunAfter` date de plus de 10 minutes. |
| `CodeCoverageMetricsAbsent` | Arrêtez l’Api et le Web (le collector garde une série 5 minutes, puis la règle attend encore 5 minutes). |
| `CodeCoverageGitHubQuotaLow` | Demande des réponses GitHub avec un `x-ratelimit-remaining` bas ; couvert par `alerts.test.yml`. |

## Production sur une VM

[`deploy/vm`](../deploy/vm) fait tourner toute l’instance sur un seul hôte Docker (M4.02) : Caddy (TLS, ACME),
PostgreSQL 18, l’Api (API REST, worker des jobs, migrations), le Web (console) et un conteneur de sauvegarde.
Des images uniquement : l’Api et le Web viennent du registre de la CI à la version d’une release, les
autres images sont publiques et épinglées par digest.

**PostgreSQL uniquement.** Une VM sous SQL Server n’a pas de recette de sauvegarde ici ; la production sous SQL Server passe
par Azure SQL et ses sauvegardes automatiques ([deployment-azure.md](deployment-azure.md)).

**Un hôte avec `COMPOSE_FILE` dans son `.env`** (l’hôte de « Déploiement continu », dont le proxy tourne dans un
conteneur) : chaque commande de cette section s’exécute sous la forme `docker compose --project-directory <directory> …`
**sans `-f`**, depuis ce répertoire (`cd <directory>` d’abord : un `COMPOSE_FILE` relatif est résolu depuis le
répertoire courant). Un `-f docker-compose.prod.yml` remplacerait `COMPOSE_FILE` et écarterait
`docker-compose.host.yml` : Caddy reviendrait, et les volumes et le réseau de l’hôte seraient
perdus. « Restauration » donne les commandes complètes de cet hôte.

### Premier déploiement

1. Une VM avec Docker (Compose 2.24 ou ultérieur), les ports 80 et 443 ouverts sur Internet, et deux noms DNS
   qui pointent vers elle : un pour l’Api (atteinte par GitHub, les agents de CI et les proxys de badges), un pour
   la console Web (ADR-011).
2. Copiez `deploy/vm` sur la VM, puis `cp .env.example .env` et renseignez chaque valeur : `IMAGE_PREFIX` et
   `TAG` (une release, sans le `v`), les noms d’hôte, `ACME_EMAIL`, `DB_PASSWORD`,
   `DP_CERT_PASSWORD`.
3. Placez le certificat Data Protection (PKCS#12) à `DP_CERT_FILE` (`./secrets/dp.pfx`). Utilisez un certificat
   géré ; `scripts/new-dp-certificate.ps1` en fabrique un auto-signé. **Sauvegardez-le à part de la
   base de données** (un gestionnaire de mots de passe, un coffre) : sans lui, les secrets stockés (clé de la GitHub App, secret
   de webhook, secret OAuth, jetons protégés) sont illisibles et doivent être ressaisis ; les données de couverture
   survivent.
4. `docker compose -f docker-compose.prod.yml pull`, puis `docker compose -f docker-compose.prod.yml up -d`.
   L’Api migre le schéma au démarrage ; le Web reste non prêt tant que le schéma n’est pas à jour.
5. Ouvrez `https://<web host>/setup` pour créer la GitHub App à partir de son manifest.
6. Installez l’App : la fin de `/setup`, l’en-tête de `/admin/organizations` et la page d’accueil vide d’un
   administrateur d’instance renvoient vers `https://github.com/apps/<slug>/installations/new`. GitHub demande
   quel compte l’installe et sur quels dépôts, puis renvoie le navigateur vers la console
   (`setup_url` du manifest ; une App créée avant M4.02i n’en a pas, définissez-la dans ses paramètres sur
   GitHub si vous voulez le retour). L’installation apparaît comme une organisation **en attente**.
7. Admettez-la depuis `/admin/organizations`, par son login : la console lit l’identifiant du compte sur GitHub et
   affiche « login → id » avant que vous ne confirmiez. Votre propre compte voit l’organisation aussitôt, sans
   se reconnecter. Si une installation n’apparaît pas (un webhook perdu pendant que l’Api était arrêtée),
   **Vérifier les installations** liste les installations de l’App immédiatement au lieu d’attendre l’exécution nocturne
   (minuit UTC).

Les appartenances d’un utilisateur connecté sont rafraîchies par le worker chaque fois qu’elles sont lues plus anciennes que
`Access:CacheMinutes`, si bien que les organisations restent listées pendant toute la vie du cookie de session ; au-delà de
`Access:HardExpiryMinutes` sans rafraîchissement réussi (GitHub indisponible, refresh token révoqué), elles n’accordent
plus rien jusqu’au prochain rafraîchissement réussi.

Caddy tourne à l’adresse fixe `172.30.0.10` du réseau `edge` (sous-réseau `172.30.0.0/24`), le seul
proxy auquel l’Api et le Web font confiance pour `X-Forwarded-For` et `X-Forwarded-Proto` (le Web en déduit le callback
https de la connexion GitHub). Pour un Caddy installé sur la VM elle-même, utilisez plutôt `Caddyfile.example`
à la racine avec le `docker-compose.yml` racine.

### Mise à jour

Indiquez le nouveau `TAG` dans `.env`, tirez les images, puis **démarrez d’abord l’Api**, qui migre le schéma, et le
Web ensuite :

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d api
docker compose -f docker-compose.prod.yml up -d web
```

Une migration ne va que vers l’avant : avant une mise à jour, faites une sauvegarde (ci-dessous, « À la demande »).

### Sauvegardes

Le service `backup` exécute `pg_dump -Fc` au démarrage puis toutes les `BACKUP_INTERVAL_HOURS` (24) dans le
volume `backups`, conserve `BACKUP_RETENTION_DAYS` jours (14), et passe **unhealthy** quand le dernier
succès date de plus de 26 heures (`docker compose ps`). Un dump contient **toutes les organisations** de
l’instance (ADR-013) : qui lit les sauvegardes les lit toutes ; restreignez le stockage en conséquence.

**Copie hors de la machine.** Les dumps sur la VM ne survivent pas à la perte de la VM. Définissez `BACKUP_HOOK` avec une
commande shell exécutée à chaque nouveau dump, son chemin dans `$1`, qui le copie vers un stockage hors de la machine (un
stockage objet, un autre hôte). L’objectif de point de reprise d’ARCHITECTURE §12.5, 24 heures au plus,
ne tient qu’avec cette copie en place.

**À la demande** (avant une mise à jour) :

```bash
docker compose -f docker-compose.prod.yml run --rm -e BACKUP_ONCE=true backup
```

### Restauration

Chaque trimestre, restaurez le dernier dump sur une VM de test et vérifiez que l’Api y est prête. La copie
restaurée contient la clé de la GitHub App et les jobs en file de la production : **son worker reste éteint**, son service
de sauvegarde n’est pas démarré et son `BACKUP_HOOK` est vide, pour qu’elle n’agisse pas sur les vrais dépôts
et n’écrase pas les copies hors machine de la production :

```bash
# On the test VM, with the same deploy/vm and a fresh database volume. In its .env: WORKER_ENABLED=false
# and BACKUP_HOOK= (empty).
docker compose -f docker-compose.prod.yml up -d db
docker compose -f docker-compose.prod.yml cp ./codecoverage-<stamp>.dump db:/tmp/restore.dump
docker compose -f docker-compose.prod.yml exec db pg_restore -U cc -d codecoverage --no-owner --exit-on-error /tmp/restore.dump
docker compose -f docker-compose.prod.yml up -d --no-deps api web   # never the backup service
```

Sur l’hôte de « Déploiement continu » (`COMPOSE_FILE` dans `.env`), restaurer un dump **à la place** de
la base de données de production, par exemple pour revenir à une version plus ancienne : la base est supprimée puis
recréée, puisque `pg_restore` laisserait les tables qu’une migration plus récente a ajoutées.

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

La VM de test a besoin du même certificat Data Protection pour lire les secrets stockés. Une copie restaurée ne fait tourner
son worker (`WORKER_ENABLED=true`) et son service de sauvegarde que lorsqu’elle **remplace** la production, une fois
l’ancienne instance arrêtée.
`scripts/test-backup-restore.ps1` exécute le même exercice sur le Docker local avec un schéma d’exemple (image de
sauvegarde, `pg_restore` dans un serveur neuf, nombres de lignes comparés) ; il est ignoré avec un motif en l’absence de
Docker.

### Montée de version majeure de PostgreSQL

Une version majeure de PostgreSQL ne lit pas les données de la précédente : la base passe par un dump
et une restauration. CodeCoverage tourne sur PostgreSQL 18 depuis M4.02e. Les images 18 rangent leurs données dans
`/var/lib/postgresql/18/docker` et montent le volume sur `/var/lib/postgresql`. Les images jusqu’à 17 le
montaient sur `/var/lib/postgresql/data`. Une image 18 refuse de démarrer sur un volume qui contient une
base 17, quel que soit le point de montage.

**Le déploiement refuse une base d’une autre version majeure.** `scripts/Deploy-Production.ps1` lit
`DATA_DIR/db/PG_VERSION` (la disposition jusqu’à 17) et `DATA_DIR/db/<major>/docker/PG_VERSION` par un
conteneur de l’image `db`, avant de rien changer. Il refuse la release quand la version diffère de l’image
`db`, et nomme cette procédure.

**Migrer l’hôte de « Déploiement continu »** (volumes en montages liés sous `DATA_DIR`), avec
[`scripts/Migrate-PostgreSqlMajor.ps1`](../scripts/Migrate-PostgreSqlMajor.ps1). Lancez-le à la main sur
l’hôte, depuis un checkout de la release qui apporte la nouvelle version, pendant que la version déployée
tourne encore :

```bash
git clone --branch v1.3.0 https://github.com/<owner>/code-coverage.git /tmp/cc-v1.3.0   # la release sur PostgreSQL 18
cd /tmp/cc-v1.3.0
pwsh scripts/Migrate-PostgreSqlMajor.ps1 -Directory /data03/docker/code-coverage            # simulation
pwsh scripts/Migrate-PostgreSqlMajor.ps1 -Directory /data03/docker/code-coverage -Execute
```

Il tourne sous l’utilisateur du runner et a besoin du groupe `docker`. Il fait, dans cet ordre :

1. Vérifie `.env` et `DATA_DIR`, l’image `db` du compose déployé (la version source) et celle du checkout
   (la version cible, plus récente), et le `PG_VERSION` de `DATA_DIR/db`. Il vérifie aussi que la base
   tourne.
2. Prend une sauvegarde (`BACKUP_ONCE`) par le service de sauvegarde du compose déployé, donc par le
   `pg_dump` de la version source, puis compte les lignes de chaque table.
3. Arrête le Web, l’Api, le service de sauvegarde, puis la base.
4. Garde les fichiers déployés dans `DEPLOY_DIR/.pg-upgrade-<stamp>`, renomme `DATA_DIR/db` en
   `DATA_DIR/db-pg17-<stamp>` et crée un `DATA_DIR/db` vide (`70:70`, mode 700). Les anciennes données ne
   sont **jamais supprimées** par le script.
5. Copie les fichiers compose, le `Caddyfile`, `.env.example` et `backup/` du checkout. `.env` et
   `secrets/` ne sont jamais touchés, et le `TAG` reste celui déployé.
6. Démarre la nouvelle base, restaure le dump avec `pg_restore` (le service de sauvegarde de la version
   cible), et compare les nombres de lignes avec la source.
7. Démarre l’Api et attend qu’elle soit saine, puis le Web, puis le service de sauvegarde.

Sur un échec après l’arrêt de la base, il revient en arrière de lui-même. Le nouveau répertoire de données
devient `DATA_DIR/db-pg18-failed-<stamp>`, l’ancien retrouve son nom, et les fichiers sont remis en place.
Puis la base, l’Api, le Web et le service de sauvegarde redémarrent sur la version source. Quand le retour
en arrière échoue aussi, l’erreur affiche les commandes pour le faire à la main.

Déployez ensuite la release (« Re-run job » de son job `deploy`) : la garde trouve maintenant une base
PostgreSQL 18. Une fois la nouvelle version en marche et une sauvegarde de PostgreSQL 18 prise, supprimez
vous-même `DATA_DIR/db-pg17-<stamp>` et `DEPLOY_DIR/.pg-upgrade-<stamp>` (le répertoire de données
appartient à l’utilisateur `70` : `sudo rm -rf`).

**Ailleurs.** Une VM sur le `docker-compose.prod.yml` simple (volumes nommés) et un `docker-compose.yml`
local suivent les mêmes étapes à la main. Prenez un dump (« Sauvegardes »), puis `down`. Supprimez l’ancien
volume `db`, ou gardez-le sous un autre nom. Puis `up -d db`, `pg_restore` comme dans « Restauration », et
démarrez l’Api, puis le Web. Un volume 17 laissé tel quel arrête la base 18 à son démarrage, quel que soit
le point de montage : le conteneur s’arrête sur une erreur au sujet des données d’une version plus ancienne.

`scripts/test-backup-restore.ps1` restaure un dump de PostgreSQL 17, de l’image de sauvegarde et du
`pg_dump` de 17, dans PostgreSQL 18.

### La ligne de commande d’administration

La ligne de commande de l’Api reste la voie de secours documentée (PLAN §11-4) : admettre une
organisation ou créer un jeton d’upload sans la console.

```bash
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
docker compose -f docker-compose.prod.yml exec api dotnet CodeCoverage.Api.dll admin create-upload-token --owner my-org --scope organization --label ci
```

Ce qu’elle fait est inscrit au journal d’audit sous l’acteur `[command-line]`.

## Déploiement continu

Chaque tag `v*` dont la CI réussit, pré-releases comprises, est déployé sur l’hôte de production par le
job `deploy` de [`ci-cd.yml`](../.github/workflows/ci-cd.yml) (M4.02b). Le job tourne sur un runner
auto-hébergé de cet hôte, dans l’environnement GitHub `production`, et attend l’approbation de son reviewer.
Il ne tourne jamais sur une pull request. Il exécute
[`scripts/Deploy-Production.ps1`](../scripts/Deploy-Production.ps1) depuis le checkout du tag sur
le compose de [`deploy/vm`](../deploy/vm), ici derrière le Nginx Proxy Manager (NPX) qui tourne déjà
sur l’hôte au lieu de Caddy.

Ce que fait un déploiement, dans cet ordre :

1. Vérifie les outils de l’hôte, puis que le commit du tag est sur `origin/main` (une version
   stable) ou sur `origin/develop` ou une branche `release/*` (une pré-release).
2. Se connecte à GHCR avec le jeton du job, dans une configuration Docker propre au job, jamais dans
   `~/.docker`.
3. Vérifie `.env`, `secrets/dp.pfx` et les répertoires de données, et compare la version au
   `TAG` actuel selon la précédence SemVer 2.0 (`1.2.0-beta.9 < 1.2.0-beta.10 < 1.2.0-rc.1 < 1.2.0`). La
   même version est un redéploiement, et une version plus ancienne est refusée. Il refuse une base de
   `DATA_DIR/db` d’une autre version majeure de PostgreSQL que l’image `db` (« Montée de version majeure de
   PostgreSQL »). Il vérifie aussi que le réseau Docker de `PROXY_NETWORK` existe.
4. Copie une liste explicite de fichiers de `deploy/vm` dans `DEPLOY_DIR`, en conservant les fichiers qu’il remplace
   jusqu’au démarrage de l’Api. Il ne touche jamais ni à `.env` ni à `secrets/`.
5. Tire les images Api et Web de la version, et les refuse sauf si leur label
   `org.opencontainers.image.revision` est le commit du tag.
6. Fait une sauvegarde (`BACKUP_ONCE`). Ce n’est qu’ensuite qu’il écrit le nouveau `TAG` dans `.env`, le précédent
   allant dans `.env.previous-tag` (un redéploiement laisse ce fichier tel quel).
7. Démarre l’Api et attend qu’elle soit saine (300 s ; une longue migration reste `starting` ou
   `unhealthy` pendant ce temps), puis fait de même pour le Web.
8. Vérifie `/health/ready` des deux hôtes à travers NPX sur le port 443 de l’hôte (`curl --resolve`,
   qui évite la boucle NAT d’un serveur domestique). Puis il compare le
   `/api/v1/uploader/linux-x64.sha256` servi à l’asset de la release.
9. Supprime les images `-api` et `-web` autres que la version actuelle, le `TAG` précédent et la
   version de `.env.previous-tag`. Il ne lance jamais de prune global : l’hôte n’est pas dédié.

Le script fonctionne par défaut à blanc, et le job passe `-Execute`. Un essai à blanc lancé à la main sur l’hôte affiche
ce qu’il ferait et ne change rien :

```bash
pwsh scripts/Deploy-Production.ps1 -Version 1.2.3 -Revision <commit of v1.2.3> -Directory /data03/docker/code-coverage
```

### Préparer l’hôte

**Outils :** PowerShell (`pwsh`) 7.4 ou ultérieur, `curl`, Docker avec le plugin compose 2.24 ou ultérieur.
Le plugin compose doit être installé **pour tout le système** (le paquet apt `docker-compose-plugin`, dans
`/usr/libexec/docker/cli-plugins` ou `/usr/lib/docker/cli-plugins`), pas dans `~/.docker/cli-plugins` :
le job fait pointer `DOCKER_CONFIG` vers un répertoire qui lui est propre, où un plugin par utilisateur n’est pas trouvé.

**Utilisateur du runner.** L’utilisateur du service du runner est dans le groupe `docker`, ce qui **équivaut à
root** sur l’hôte : quiconque exécute un job sur ce runner contrôle l’hôte. D’où le label dédié,
le hook de pré-job et, surtout, le ruleset de tags ci-dessous.

**Ce qui peut tourner sur le runner est décidé par qui peut pousser un tag `v*`.** Le hook accepte tout job du
`ci-cd.yml` d’un tag `v*`, et ce fichier vient du tag lui-même : un tag peut porter un `ci-cd.yml` avec
un job qui cible `cc-production` et ne déclare aucun environnement. Le reviewer requis de
`production` ne retient que le job `deploy` ; il ne protège pas le runner. Le ruleset de tags sur
`refs/tags/v*` (« Paramètres GitHub » ci-dessous) est donc **obligatoire**, en place avant que le runner reçoive
son label, et vérifié à nouveau chaque fois que les collaborateurs du dépôt changent.

**Répertoire de production** (`DEPLOY_DIR=/data03/docker/code-coverage`, `DATA_DIR=$DEPLOY_DIR/data`) :

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

**`.env`.** Il se remplit comme dans « Premier déploiement » ci-dessus, plus les lignes de la variante NPX de
[`.env.example`](../deploy/vm/.env.example) :

- `COMPOSE_FILE=docker-compose.prod.yml:docker-compose.host.yml` : compose le lit dans `.env`, si bien que
  chaque commande est un simple `docker compose --project-directory /data03/docker/code-coverage …`, lancé
  **depuis ce répertoire** (`cd /data03/docker/code-coverage` d’abord). Compose résout un chemin relatif de
  `COMPOSE_FILE` depuis son répertoire courant, pas depuis `--project-directory` : lancé d’ailleurs, il échoue avec
  `stat <répertoire courant>/docker-compose.prod.yml: no such file or directory`. La forme absolue
  (`/data03/docker/code-coverage/docker-compose.prod.yml:/data03/docker/code-coverage/docker-compose.host.yml`)
  fonctionne de partout. Le script de déploiement accepte les deux : il lance chaque commande compose depuis
  `DEPLOY_DIR` et revient ensuite à son propre répertoire, échec compris (M4.02c).
- `PROXY_NETWORK` : le réseau Docker existant de NPX.
- `PROXY_NETWORK_CIDR` : son sous-réseau, obtenu par
  `docker network inspect <network> --format '{{(index .IPAM.Config 0).Subnet}}'`.
- `DATA_DIR=/data03/docker/code-coverage/data`.

`ACME_EMAIL` n’y sert pas mais doit garder une valeur, car compose vérifie chaque variable de ses
fichiers.

[`docker-compose.host.yml`](../deploy/vm/docker-compose.host.yml) fait quatre choses :

- il désactive Caddy ;
- il raccorde l’Api et le Web au réseau NPX sans publier de port ;
- il remplace `ForwardedHeaders__KnownProxies` par `ForwardedHeaders__KnownNetworks=${PROXY_NETWORK_CIDR}` :
  l’adresse du conteneur NPX change quand il est recréé, donc l’Api et le Web font confiance aux en-têtes
  `X-Forwarded-*` de tout le sous-réseau. Ne rattachez que des conteneurs de confiance à ce réseau ;
- il monte en bind les volumes sous `DATA_DIR`.

`.env` et `dp.pfx` restent sur l’hôte, jamais dans GitHub.

**Premier déploiement, à la main.** Les images d’un dépôt privé sont privées :

1. Connectez-vous une fois avec un personal access token classique limité à `read:packages`
   (`docker login ghcr.io`).
2. Copiez `deploy/vm` dans `DEPLOY_DIR`.
3. Lancez `cd /data03/docker/code-coverage`, puis `docker compose --project-directory . pull`, puis
   `docker compose --project-directory . up -d`.
4. Supprimez le jeton ensuite (`docker logout ghcr.io`).

Les déploiements suivants se connectent avec le jeton du job. Les volumes montés en bind doivent exister
avant le premier `up`. Un hôte qui a déjà fait tourner le simple `docker-compose.prod.yml` a des volumes nommés
des mêmes noms avec d’autres options, et compose les refuse. Sauvegardez, supprimez les anciens volumes, et
restaurez dans la nouvelle base de données (voir « Restauration »).

### Les proxy hosts NPX

Ajoutez deux proxy hosts dans NPX, tous deux sur le réseau nommé dans `PROXY_NETWORK` :

| | Api | Web |
|---|---|---|
| Domain name | `CC_API_HOST` | `CC_WEB_HOST` |
| Scheme, forward host, port | `http`, `code-coverage-api-1`, `8080` | `http`, `code-coverage-web-1`, `8080` |
| Websockets support | inutile | **activé** (Blazor Server garde un WebSocket ouvert) |
| SSL | un certificat Let’s Encrypt, Force SSL, HTTP/2 | idem |

Le forward host est le nom de conteneur donné par compose (projet `code-coverage`), jamais le nom de service
`api` ou `web` : d’autres piles sur le réseau partagé peuvent utiliser le même nom. NPX envoie
`X-Forwarded-Proto` et `X-Forwarded-For` par défaut. Le Web en a besoin pour construire le callback
`https` de la connexion GitHub, et l’Api ne leur fait confiance que depuis `PROXY_NETWORK_CIDR`. Un upload
refusé avec 413 par NPX demande un `client_max_body_size` plus grand dans la configuration avancée du
proxy host de l’Api.

### Le runner

- **Label dédié.** Ajoutez le label `cc-production` au runner : le job cible
  `[self-hosted, cc-production]`, jamais `self-hosted` seul. Ne faites tourner aucun autre runner sur cet hôte.
- **Hook de pré-job.** Installez
  [`deploy/vm/runner/job-started.sh`](../deploy/vm/runner/job-started.sh) en tant que root, hors de portée de
  l’utilisateur du runner (par exemple `/opt/actions-runner-hooks/job-started.sh`, appartenant à root, mode 755). Nommez-le
  dans le `.env` du répertoire du runner, puis redémarrez le service du runner :

  ```bash
  ACTIONS_RUNNER_HOOK_JOB_STARTED=/opt/actions-runner-hooks/job-started.sh
  CC_DEPLOY_REPOSITORY=<owner>/code-coverage
  ```

  Le hook fait échouer tout job qui ne remplit pas toutes ces conditions :
  - il vient de ce dépôt ;
  - son événement est `push` ;
  - sa ref est `refs/tags/v*` ;
  - son `GITHUB_WORKFLOW_REF` commence par
    `<owner>/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v`.

  Une pull request qui cible le label est donc refusée avant sa première étape. Sans
  `CC_DEPLOY_REPOSITORY`, tout job est refusé. `sh deploy/vm/runner/test-job-started.sh` le teste
  hors ligne, et la CI exécute ce test.

### Outils de mise à jour d’images de l’hôte

Seul un tag `v*` déployé par le job change une image de CodeCoverage. Chaque image de
[`docker-compose.prod.yml`](../deploy/vm/docker-compose.prod.yml) est épinglée, par condensé ou par le `TAG`
d’une release, et chaque service porte deux labels (M4.02d) :

- `wud.watch: "false"` : WUD (What’s Up Docker) ne surveille pas le conteneur ;
- `com.centurylinklabs.watchtower.enable: "false"` : Watchtower ne le met pas à jour.

Ils ne changent rien sur un hôte sans ces outils. Sans eux, un tel outil recrée de lui-même un conteneur
sur une image plus récente. Le 2026-11-14, WUD a recréé le conteneur de la base en PostgreSQL 18 une
minute après son initialisation par PostgreSQL 17. La nouvelle version majeure a refusé les données, et l’Api
ne joignait plus `db`.

Un label est posé quand compose crée le conteneur. Les conteneurs créés avant les labels les reçoivent
à leur prochaine recréation. Le déploiement recrée l’Api et le Web, et la base par `up -d api` quand sa
configuration a changé. Le service de sauvegarde n’est recréé que lorsque `backup/` change.
**Avant de relancer WUD ou Watchtower** sur l’hôte :

1. Recréez ce qui n’a pas encore les labels, depuis le répertoire de production (les conteneurs dont la
   configuration a changé redémarrent, la base comprise : une courte interruption) :

   ```bash
   cd /data03/docker/code-coverage
   docker compose --project-directory . up -d
   ```

2. Vérifiez que chaque conteneur du projet porte les deux labels :

   ```bash
   docker ps --filter label=com.docker.compose.project=code-coverage \
     --format '{{.Names}} wud={{.Label "wud.watch"}} watchtower={{.Label "com.centurylinklabs.watchtower.enable"}}'
   ```

   Chaque ligne doit finir par `wud=false watchtower=false`.

3. Lancez l’outil, puis vérifiez qu’il ne recrée aucun conteneur de CodeCoverage : `docker ps` garde
   leurs dates de création, et WUD ne les liste pas comme surveillés.

### Paramètres GitHub (propriétaire)

- **Environnement `production` :**
  - sous les branches et tags de déploiement, remplacez la règle de branche `v*` par une règle de **tag** `v*.*.*` ;
  - ajoutez le propriétaire comme **reviewer requis**. Le job attend l’approbation avant de démarrer, et
    l’attente ne compte pas dans son `timeout-minutes` (20). Une approbation en attente expire au bout de 30 jours,
    et le job échoue alors ;
  - ajoutez la variable `DEPLOY_DIR` = `/data03/docker/code-coverage` ;
  - ajoutez la variable `PRODUCTION_URL` = l’adresse https du Web (`https://<CC_WEB_HOST>`), le
    lien que l’exécution affiche pour l’environnement. Elle est facultative : non définie, l’exécution n’affiche aucun lien.
- **Ruleset de tags** sur `refs/tags/v*` (**obligatoire**, voir « Préparer l’hôte ») : seul le propriétaire peut
  créer, déplacer ou supprimer un tel tag. C’est un tag qui déploie, et c’est son `ci-cd.yml` qui tourne sur le
  runner.
- **Packages** `code-coverage-api` et `code-coverage-web` sur GHCR : sous « Manage Actions access »,
  donnez à ce dépôt le rôle `Read`, pour que le jeton du job puisse les tirer.

### Redéployer, revenir en arrière, rafales

- **Redéployez** la même version avec « Re-run job » sur l’exécution du tag, qui conserve le contexte du
  tag. Il n’y a pas de `workflow_dispatch`.
- **Revenir à une version plus ancienne.** Une migration ne va que vers l’avant, c’est donc une opération manuelle sur
  l’hôte :
  1. arrêtez le Web et l’Api ;
  2. restaurez le dump fait avant le déploiement (« Restauration », les commandes d’un hôte avec
     `COMPOSE_FILE`) ;
  3. extrayez l’ancien tag dans un répertoire à vous (`git clone --branch v1.2.2 …`) : son
     `scripts/Deploy-Production.ps1` copie le `deploy/vm` de son propre checkout (`-Source` y pointe
     par défaut), si bien que les fichiers correspondent à la version ;
  4. connectez-vous à GHCR avec un jeton classique limité à `read:packages` (`docker login ghcr.io`) : le
     script tire toujours les images ;
  5. lancez ce script avec l’ancienne version, son commit, `-AllowDowngrade` et `-Execute` :

     ```bash
     pwsh scripts/Deploy-Production.ps1 -Version 1.2.2 -Revision <commit of v1.2.2> -Directory /data03/docker/code-coverage -AllowDowngrade -Execute
     ```

  6. déconnectez-vous ensuite (`docker logout ghcr.io`).
- **Un échec avant le démarrage de l’Api** remet le `TAG` précédent et les fichiers de `deploy/vm` qu’il
  avait remplacés ; les conteneurs en marche n’ont pas été touchés. Un dump fait par l’exécution en échec reste dans les
  sauvegardes.
- **Un échec après le démarrage de l’Api** n’est pas annulé automatiquement, car le schéma a pu
  migrer. Le job échoue et indique le répertoire de données, le dump et les étapes, dont les commandes compose
  commencent par `cd <DEPLOY_DIR>` (un `COMPOSE_FILE` relatif) :
  1. arrêtez l’Api et le Web ;
  2. lancez `pg_restore` du dump ;
  3. réglez `TAG` sur le contenu de `.env.previous-tag` (un redéploiement de la même version laisse ce
     fichier tel quel, il désigne donc toujours la version à laquelle revenir) ;
  4. lancez `up -d api`, puis `up -d web`.

  Les images de la version actuelle, du `TAG` précédent et de la version de
  `.env.previous-tag` ne sont jamais supprimées par le nettoyage, pour que le retour les trouve sur l’hôte.
- **Une rafale de tags.** Les déploiements se mettent en file dans le groupe de concurrence `production` et ne sont jamais
  annulés, mais GitHub ne garde que celui en cours et le dernier en attente. Un tag plus ancien que la
  version déployée est alors refusé (« older than the deployed »), comme prévu.

### Avant de rendre le dépôt public

C’est obligatoire : un runner auto-hébergé d’un dépôt public peut être atteint par les pull requests
de forks.

- Sous « Fork pull request workflows from outside collaborators », exigez une approbation pour **tous** les
  collaborateurs extérieurs.
- Vérifiez que le ruleset de tags sur `refs/tags/v*` est actif et ne laisse que le propriétaire créer, déplacer ou
  supprimer un tag. C’est une vérification bloquante, pas un point de revue : un tag décide de ce qui tourne sur le runner.
- Gardez le hook et le label dédié en place, ou désinscrivez le runner.
- Revoyez la visibilité des packages GHCR.
