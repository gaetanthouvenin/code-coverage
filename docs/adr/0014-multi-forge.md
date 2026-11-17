# ADR-014 : Plusieurs forges

- **Statut :** accepté le 2026-11-09 (M1.16a2). **Amendement M7.02** proposé le 2026-11-16 et **accepté par le propriétaire le 2026-11-16** : implémentation d'Azure DevOps, de Bitbucket et de GitLab.com, choix d'Azure DevOps. Complète [ADR-013](0013-organization-isolation.md) ; les connexions de forge sont tranchées par [ADR-017](0017-forge-connections.md).
- **Date :** 2026-11-08, amendé le 2026-11-16 (M7.02)
- **Références :** `docs/PLAN.md` §1.4, §2.1, §2.3, §2.7 à §2.9, §4.4, §9, §9.1 à §9.3, §10 et §11 ; `docs/ARCHITECTURE.md` §1.3, §4.2, §5.1, §8, §10.2 et §10bis.3, réécrits par M1.16a2 ; `docs/research/azure-devops-diff.md` (spike M7.01)

## Contexte

- Le produit ne connaît que GitHub : `ARCHITECTURE.md` §1.3 classe GitLab et Bitbucket hors périmètre, la règle 2 d'`AGENT.md` tire le diff de l'API GitHub, et ADR-013 définit une organisation comme un compte GitHub.
- Une instance doit pouvoir servir des organisations réparties sur GitHub, Azure DevOps, Bitbucket et GitLab.com.
- Rien de ce qui fige cette hypothèse n'est encore construit : `Organizations` n'est pas livrée, et ni la porte d'entrée (M1.16c), ni les dépôts (M1.17), ni le port de forge (M1.24), ni les routes ne sont écrits. C'est le moment le moins coûteux pour rendre l'identité et les adresses indépendantes de la forge.
- Le coût réel n'est pas dans une colonne, qu'une migration ajoute à tout moment, mais dans les adresses et les ports, qui ne se changent plus une fois publiés et consommés.

## Décision

**Préparer d'abord, puis implémenter toutes les forges.** L'identité, les adresses et le port principal sont devenus indépendants de la forge avant la bascule (M1.16a2 à M1.16a4), avec GitHub seul implémenté jusque-là.

**Amendement M7.02 (décision du propriétaire du 2026-11-14).** La première décision disait « préparer maintenant, implémenter GitHub seul ». Elle est remplacée : Azure DevOps Services (M7), puis Bitbucket Cloud (M8), puis GitLab.com (M9) sont **implémentés**, pas seulement préparés, dans cet ordre. M7, première forge après GitHub, porte aussi les refactors neutres que M8 et M9 réutilisent : ports à clé `Forge`, connexions ([ADR-017](0017-forge-connections.md)), admission, réception et traitement des webhooks, hooks, santé des connexions. Les choix propres à Azure DevOps sont dans la section « Azure DevOps : connexion, notifications et diff » ci-dessous.

### Arbitrages du propriétaire

| # | Question | Décision |
|---|---|---|
| 1 | Quelles forges ? | **Azure DevOps Services, Bitbucket Cloud et GitLab.com**, en plus de GitHub. GitLab auto-hébergé et les versions serveur restent hors portée : l'identité ne porte pas d'URL d'instance. |
| 2 | La forge entre-t-elle dans les adresses ? | **Oui**, dans toute adresse d'organisation. Les URL compatibles Codecov ne changent pas. |
| 3 | Les logins réservés restent-ils nécessaires ? | **Non.** L'owner n'est plus le premier segment d'une route. Retrait en M3.43a. |
| 4 | Comment loger le projet d'Azure DevOps et les sous-groupes de GitLab ? | **Colonne `Namespace` sur `Repositories`**, et owner adressé avec `:` (`org:project`, `group:sub:sub2`). Le tenant est ce qui précède le premier `:`. |
| 5 | Le port GitHub devient-il neutre ? | **Oui, pour ce que l'Application consomme.** Ce qui est propre à GitHub reste derrière `IGitHubAppGateway`. |
| 6 | Identité des utilisateurs ? | **`(Forge, ExternalId)`, sans fusion de comptes.** Les administrateurs d'instance restent des identifiants GitHub. |
| 7 | Credentials d'upload ? | **Jeton partout.** OIDC : GitHub d'abord, Bitbucket et GitLab ensuite, pas sur Azure DevOps. |
| 8 | Les autres forges sont-elles implémentées ? (amendement M7.02, 2026-11-14) | **Oui, toutes**, dans l'ordre Azure DevOps (M7), Bitbucket (M8), GitLab.com (M9). Chaque refactor neutre est une micro-feature placée avant l'adaptateur qui l'utilise, et GitHub ne régresse pas. |

### Identité

- Une organisation est **un compte d'une forge** : `Forge` (`GitHub`, `AzureDevOps`, `Bitbucket`, `GitLab`, stocké en texte) et `ExternalAccountId`, texte de 40 caractères au plus, enregistré en minuscules. Ce texte couvre l'identifiant numérique de GitHub comme un GUID ou un UUID entre accolades ; les minuscules donnent le même comportement à l'index unique sous les collations de PostgreSQL et de SQL Server.
- `UX(Forge, ExternalAccountId)` et `UX(Forge, NormalizedLogin)`, sans filtre. Un même nom sur deux forges désigne deux organisations distinctes.
- `Login` compte 255 caractères au plus, la longueur d'un chemin GitLab. La règle de forme reste unique pour toutes les forges : une lettre ou un chiffre ASCII, puis des lettres, chiffres, `-`, `_` ou `.`. Un login qu'aucune forge n'émettrait ne résout simplement rien, et `~`, `:` et `/` restent refusés.
- La pierre tombale d'ADR-013 devient `~stale-{ExternalAccountId}` et ne vise que le détenteur du login **sur la même forge**.
- **Précision sur ADR-013 :** la pose d'une pierre tombale ne met pas de réconciliation en file. Aucun type de job n'existe pour cela au moment de M1.16b, et la synchronisation `SyncInstallations` (M2.03a1) rétablit le login. La ligne reste inatteignable d'ici là.
- `Repositories` porte `ExternalId` en texte à la place de `GitHubId` : `UX(OrganizationId, ExternalId)`. La forge n'y figure pas, une organisation n'appartenant qu'à une seule.
- `Users` est identifié par `(Forge, ExternalId)`. Une personne présente sur deux forges a deux comptes. `Access:AdminUserIds` reste une liste d'identifiants GitHub.

### Adresses

| Usage | Forme |
|---|---|
| Web, dépôt | `/{forge}/{owner}/{repo}/…` |
| Web, organisation | `/orgs/{forge}/{org}/…` |
| API, dépôt | `/api/v1/repos/{forge}/{owner}/{repo}/…` |
| API, organisation | `/api/v1/orgs/{forge}/{org}/…` |
| Badges de l'API | `/api/v1/badges/{forge}/{owner}/{repo}.{ext}` |
| Badges compatibles Codecov | inchangés : `/gh/…`, `/github/…`, `/bb/…`, `/bitbucket/…` à partir de M8, et `/gl/…`, `/gitlab/…` à partir de M9, avec un owner en `group:sub` |
| Upload | `POST /api/v1/uploads` inchangé, avec un champ `forge` facultatif, `github` par défaut |

- Valeurs de `{forge}` : `github` (alias `gh`), `bitbucket` (alias `bb`), `azure-devops`, `gitlab` (alias `gl`).
- `IOrganizationScope.TryEnterAsync` reçoit la forge et le login, et `IRepositoryScoped`, `IOrganizationScoped` et `IPublicOwnerAddressed` portent la forge.
- Un uploader existant, qui n'envoie pas de champ `forge`, continue de fonctionner sans changement.

### Azure DevOps : le niveau projet

- Azure DevOps s'organise en organisation, projet et dépôt, là où les autres forges n'ont que deux niveaux.
- Le tenant reste **l'organisation** Azure DevOps. Le projet est rangé dans `Repositories.Namespace`, chaîne vide sur les autres forges, ce qui garde l'index non filtré : `UX(OrganizationId, NormalizedNamespace, NormalizedName)`.
- Le segment owner vaut `org:project` sur Azure DevOps. Le caractère `:` est interdit dans un nom de projet, donc la forme est sans ambiguïté, et une seule famille de routes sert toutes les forges. La porte lit l'organisation avant les deux-points.

### Azure DevOps : connexion, notifications et diff (amendement M7.02)

Azure DevOps n'a pas de référence Codecov (règle critique 1) : son comportement suit celui de GitHub, et chaque écart est nommé dans « Écarts avec Codecov ». La classe de chaque fait (prouvé ou probable) est donnée dans « Prouvé et probable ».

- **Identité de l'instance : une application Entra ID multi-tenant.** L'instance enregistre une seule application Microsoft Entra ID multi-tenant. Chaque organisation Azure DevOps admise y consent dans son tenant, puis ajoute le principal de service de l'application comme utilisateur de l'organisation, avec un accès **Basic** (Stakeholder n'ouvre pas Azure Repos). L'application lit les dépôts, publie les statuts et les commentaires, et gère les abonnements de service hooks. Ses identifiants sont ceux de la connexion de l'organisation ([ADR-017](0017-forge-connections.md)). L'accès Basic d'une identité d'application est payant dans chaque organisation : c'est une action et un coût du client, documentés par l'onboarding (M7.57).
- **Identifiant de l'organisation :** `instanceId` de `_apis/connectionData`, un GUID, enregistré en minuscules dans `ExternalAccountId` (M7.40). Ce point est **probable** (spike M7.01, §6.2) : l'endpoint n'est pas dans la référence REST.
- **Connexion Web : Entra ID OpenID Connect.** Un membre se connecte avec son compte Entra, par OpenID Connect, et reçoit un jeton délégué renouvelé par Entra (M7.52, M7.53). L'OAuth propre à Azure DevOps n'est pas employé : il est déprécié, n'accepte plus de nouvelle application depuis avril 2025 et doit disparaître en 2026. Son identité est `(AzureDevOps, id de profil)`, sans fusion avec un compte GitHub (arbitrage 6).
- **Comptes Microsoft personnels refusés.** Les applications Entra ne prennent pas en charge les comptes Microsoft personnels pour la ressource Azure DevOps. Un tel compte est refusé à la connexion avec un message traduit (M7.53), tant que Microsoft n'a pas livré cette prise en charge.
- **Ce que ces deux choix exigent d'une organisation Azure DevOps.** Le spike M7.01 ne traite pas cette question : sa mesure emploie le jeton d'accès personnel (PAT) d'un membre, sans application Entra (`tools/azure-devops-diff/README.md`, §3). Ce qui suit vient de la documentation Microsoft, consultée le 2026-11-16, sauf mention « non prouvé ».
  - **L'organisation doit être reliée à un annuaire Microsoft Entra ID** (prouvé). Le principal de service de l'application doit exister dans le tenant relié à l'organisation ; le consentement à l'application multi-tenant l'y crée. Il doit ensuite être ajouté explicitement aux utilisateurs de l'organisation, avec un accès Basic.
  - **Qui peut l'ajouter** (prouvé) : un membre de « Project Collection Administrators », ou un administrateur de projet ou d'équipe quand la politique d'invitation le permet. Les service hooks exigent les permissions « Edit subscriptions » et « View subscriptions » de chaque projet, que seuls les administrateurs de projet ont par défaut.
  - **Le consentement exige probablement un administrateur du tenant** (non prouvé : cela dépend de la politique de consentement du tenant ; l'onboarding de M7.57 décrit l'étape et le pilote M7.58 la constate sur un tenant réel).
  - **Les membres qui se connectent au Web ont un compte Entra du tenant relié.** Un compte Microsoft personnel qui a accepté une invitation comme invité B2B de ce tenant peut ouvrir Azure DevOps, si le tenant autorise l'accès des invités (prouvé). Le refus des comptes personnels par une application Entra laisse probablement ce cas refusé par la connexion Web de M7.53 (non prouvé ; aucune micro-feature ne le vérifie encore : à ajouter aux tests de M7.53 ou à constater au pilote M7.58).
- **Une organisation non reliée à un annuaire Entra ne peut pas être admise.** C'est une organisation « adossée à des comptes Microsoft », qui sert surtout des comptes Microsoft personnels.
  - L'application de l'instance ne peut pas y être ajoutée : un principal de service n'est admis que dans une organisation reliée à un tenant Entra (prouvé). L'admission échoue donc. Ce refus est un écart nommé et non configurable (« Écarts d'Azure DevOps avec GitHub ») : l'onboarding de M7.57 décrit la condition et sa vérification. La vérification de M7.40 le rend probablement par le refus d'accès générique (`TF400813`), sans message propre à ce cas (probable, non mesuré).
  - Ses membres à compte personnel ne peuvent pas se connecter au Web (prouvé, voir « Comptes Microsoft personnels refusés »). Une telle organisation peut aussi admettre des comptes Entra, par leur UPN ; savoir si la connexion Web de M7.53 leur donne un jeton délégué utilisable sur cette organisation reste non prouvé, et la documentation de Microsoft se contredit sur l'accès des comptes Entra à une organisation non reliée.
  - **Comment savoir si une organisation remplit la condition** (prouvé) : avec au moins un accès Basic, ouvrir **Organization settings > Microsoft Entra ID** (`https://dev.azure.com/{organisation}/_settings/organizationAad`). La page propose **Disconnect directory** si l'organisation est reliée, **Connect directory** sinon.
  - **Comment la rendre éligible** (prouvé) : un membre de « Project Collection Administrators », lui-même *membre* (pas invité) du tenant de destination, relie l'organisation par **Connect directory**. Chaque membre se reconnecte ensuite avec son identité Entra ; un membre sans adresse dans l'annuaire est rattaché ou invité comme invité. Au-delà de 100 utilisateurs, la liaison reste possible, mais l'outil de rattachement des utilisateurs déconnectés ne fonctionne plus et le support de Microsoft les rattache. Les clés SSH sont à supprimer avant la bascule, par chaque utilisateur ou à la demande auprès du support ; une clé restante peut empêcher d'en ajouter une nouvelle, et seul le support l'efface alors.
  - **Cas d'une personne seule avec un compte personnel.** Il lui faut un tenant Entra dont elle est membre. Un tenant Entra ID Free s'obtient en ouvrant un compte Azure gratuit, et son créateur en devient administrateur général (prouvé). Deux points restent non prouvés, à vérifier avant le pilote M7.58 : que cette inscription soit possible avec un compte Microsoft personnel, et que ce compte soit alors *membre*, et non invité, du tenant, comme l'exige **Connect directory**.
  - **Ce qui reste possible sans annuaire** : la mesure du spike M7.01 n'emploie qu'un PAT. La documentation de Microsoft traite le PAT pour toute organisation et ne réserve aux organisations reliées que ses politiques de restriction. Le PAT est donc accepté sur les deux sortes d'organisation, mais la mesure n'a pas été exécutée sur une organisation non reliée. Seule sa deuxième exécution, avec le jeton de l'application (`-TokenKind Bearer`, question 4), exige une organisation reliée.
- **Service hooks : un secret d'en-tête propre à la connexion, charge utile `Minimal`.** Azure DevOps ne signe pas ses service hooks par HMAC. Chaque abonnement (consommateur `webHooks`, portée `Project`) envoie un en-tête qui porte le secret de webhook de la connexion. La réception le compare en temps constant au secret courant et au précédent (rotation, M7.13), sur la route `/api/v1/azure-devops/webhooks/{connectionId}` ([ADR-017](0017-forge-connections.md)). La charge utile est demandée en `resourceDetailsToSend=Minimal`, avec un `resourceVersion` explicite. Le traitement relit l'état par l'API avec l'identité de l'application, et ne fait jamais confiance au corps (M7.49 à M7.51). Une livraison forgée avec un secret volé ne peut donc que déclencher une relecture, sans injecter d'état. Créer un abonnement exige le droit d'administration du projet ; un projet sans ce droit est signalé « interdit » sur la page de connexion, sans faire échouer la synchronisation (M7.51).
- **Statuts : `genre=codecov`.** Chaque contexte émis par `NotificationService` (`codecov/project`, `codecov/patch`, `codecov/changes`, ceux des flags) est découpé au premier `/` : `genre` = `codecov`, `name` = le reste. Le statut est publié sur le commit et, quand une pull request active a ce commit pour tête, sur son itération (M7.47). Une stratégie de branche exige alors `codecov/patch` sous la forme `genre/name`, le même nom que sur GitHub. La pull request active est trouvée par la liste des pull requests actives de la branche, filtrée sur `lastMergeSourceCommit` ; `pullrequestquery` ne promet pas de trouver une tête active (spike M7.01, §6.3, **probable**).
- **Commentaire : un fil de pull request unique.** Le commentaire Codecov est le seul commentaire d'un fil créé par l'application. Le fil est retrouvé par le marqueur `<!-- code-coverage:comment -->` et par le compte de service de la connexion, puis mis à jour en place. L'identifiant `long` de `ForgeIssueComment` et de `PullRequests.CommentId` est l'**identifiant du fil**, et le commentaire unique a l'identifiant 1 (M7.48). Ce point est **probable** : la référence Microsoft se contredit sur la numérotation des commentaires (spike M7.01, §6.1). Si la mesure du spike prouve le contraire, une micro-feature d'identifiant de commentaire texte est ajoutée avant M7.48.
- **Upload : par jeton seulement.** Un upload `forge=azure-devops` s'authentifie par un jeton d'organisation ou de dépôt. Un JWT OIDC est refusé avec un code explicite (M7.55), conformément à l'arbitrage 7.
- **Diff : voie C du spike M7.01, provisoire jusqu'à la mesure.**
  - `Diffs - Get` avec `diffCommonCommit=true`, toutes les pages lues, donne les fichiers, leurs identifiants d'objet et la base : le `commonCommit` entre `lastMergeTargetCommit` et la tête de la pull request, jamais le commit de fusion synthétique (règle critique 2) ni le `commonRefCommit` de l'itération, qui peut être plus ancien ;
  - `Blobs - Get` lit les deux versions de chaque fichier ;
  - un portage C# de l'algorithme xdiff de git, dans `Infrastructure/AzureDevOps`, produit un vrai patch unifié que `UnifiedDiffParser` lit sans changement. Aucun paquet tiers.

  Le patch est **inconnu, jamais vide**, pour un fichier binaire (octet NUL dans les 8 000 premiers octets, la règle de git), pour un côté au-delà du plafond (1 Mio, valeur de départ à confirmer par le coût mesuré) et pour un blob illisible. `Truncated` est posé quand `allChangesIncluded` vaut `false` ou au-delà de 3 000 fichiers, la limite de GitHub. `FileDiffs` n'est pas la source : c'est une préversion, sans texte de ligne, dont l'algorithme n'est pas celui de git.

  **Condition :** la voie est prouvée localement (300 éditions sur 300 identiques à `git diff -U0`), pas encore sur des données Azure DevOps. L'acceptation de M7.01 exige qu'elle reproduise `git diff -U0` sur au moins neuf pull requests réelles sur dix. Tant que le propriétaire n'a pas exécuté la mesure, M7.46 ne commence pas. Si la mesure échoue, cette section est amendée avant M7.46.

### GitLab.com : groupes et sous-groupes

- Une organisation GitLab est un **groupe de premier niveau** ou un espace personnel (`OrganizationAccountType.User`). Un sous-groupe n'est jamais un tenant. `ExternalAccountId` porte l'identifiant numérique du namespace, en texte.
- Le segment owner s'écrit `group:sub:sub2`. C'est la règle d'Azure DevOps : le tenant est ce qui précède le premier `:`, le reste est le `Namespace` du dépôt, stocké sous la forme `sub/sub2`. Le caractère `:` n'est permis dans aucun chemin GitLab.
- `Namespace` compte 512 caractères au plus. GitLab permet 20 niveaux de 255 caractères, et un index SQL Server est limité à 1 700 octets ; un dépôt au-delà de la borne n'est pas suivi.
- À l'upload, le slug est `group/sub/repo`, découpé au dernier `/`, ou la forme de Codecov `group:::sub::::repo`.
- **Diff :** `GET /projects/:id/repository/compare` avec `unidiff=true`. Les indicateurs `collapsed`, `too_large` et `compare_timeout` signalent des diffs absents : un diff incomplet donne un patch inconnu, jamais un patch vide.
- **Statuts et commentaires :** `POST /projects/:id/statuses/:sha` avec `name`, réessayé sur 409, et notes de merge request.
- **Webhooks :** signature HMAC. Les webhooks de groupe étant réservés à Premium, le cas général est un webhook par projet.
- **OIDC :** `id_tokens` existe sur tous les plans. `namespace_id` désigne le namespace direct, et aucun claim ne donne la racine : le lien se fait sur `project_id` égal à `ExternalId`, jamais sur `namespace_id`. Dans une merge request depuis un fork, le claim désigne le projet source : l'upload échoue fermé.

### Ports

- `IForgeGateway` remplace `IGitHubAppGateway` pour ce que l'Application consomme : lecture des dépôts, commits, comparaisons, arbres et contenus, fichiers d'une pull request, statuts et commentaires. Ses contrats sont dans `Domain/Contracts/Forges/`. Il est résolu par `IForgeGatewayProvider`.
- Chaque méthode au niveau d'un dépôt reçoit une référence neutre : forge, connexion (l'identifiant d'installation sur GitHub) et identifiant externe du dépôt.
- **Restent propres à GitHub :** `IGitHubAppGateway` pour les installations, la conversion du manifest et les Check Runs ; `GitHubWebhookEvent` et la réception des webhooks ; `IGitHubUserGateway`. Le jeton délégué d'un utilisateur passe par le port neutre `IForgeUserTokenProvider`, résolu par `IForgeGatewayProvider.GetUserTokenProvider(Forge)` (M7.16). `Installations` reste une table GitHub. La table de connexions `ForgeConnections` arrive avec M7 ; elle porte les secrets d'accès par organisation ([ADR-017](0017-forge-connections.md)).
- On ne construisait aucune abstraction de webhooks ni de connexion tant qu'une seule forge existait. **Amendement M7.02 :** Azure DevOps est ce deuxième adaptateur. M7 construit les ports neutres (annuaire, utilisateur, jeton, liens, vérification de connexion, validation et traduction des webhooks, hooks), enregistrés en services à clé `Forge`, à partir des deux cas réels, GitHub et Azure DevOps.

### Règles critiques

- **Règle 1.** La compatibilité avec Codecov vaut pour GitHub, Bitbucket et GitLab. Azure DevOps n'a pas de référence : son comportement suit celui de GitHub, et ses écarts sont nommés ci-dessous.
- **Règle 2.** Le diff vient toujours de l'API de la forge, jamais de l'historique git de l'agent de CI. La règle est reformulée par M1.16a2.

## Écarts avec Codecov

| Écart | Configurable |
|---|---|
| L'API de gestion et les pages Web portent la forge. Les URL de badge et l'upload compatibles Codecov ne changent pas. | **Non**, par conception |
| Azure DevOps n'est pas documenté par Codecov comme hébergeur de code : ses statuts, ses badges et ses commentaires n'ont pas de référence à reproduire. | Non : aucune référence |
| Sur Azure DevOps, l'upload se fait par jeton seulement : pas d'OIDC d'upload, un JWT est refusé avec un code explicite. | **Non**, jusqu'à preuve d'un OIDC exploitable |
| Un ancien chemin GitLab commençant par `_` ou `.` ne peut pas être admis. | Non : règle de forme commune |
| Un dépôt GitLab dont les sous-groupes dépassent 512 caractères n'est pas suivi. | Non : borne de l'index |

### Écarts d'Azure DevOps avec GitHub (amendement M7.02)

Azure DevOps suit GitHub ; chaque différence est nommée ici et n'est **pas configurable**.

| Écart | Raison |
|---|---|
| Pas de route de badge compatible Codecov : seule la route neutre `/api/v1/badges/azure-devops/{owner}/{repo}.{ext}` existe, et les extraits de badge la proposent. | Codecov n'a pas de segment Azure DevOps à reproduire. |
| Pas de signature HMAC des webhooks : un service hook est authentifié par un secret d'en-tête propre à la connexion, comparé en temps constant, et son corps `Minimal` n'est jamais cru sans relecture par l'API. | Azure DevOps ne signe pas ses service hooks. |
| Pas d'OIDC d'upload (voir le tableau précédent). | Le jeton d'Azure Pipelines n'est pas conçu pour un service tiers (probable). |
| Les comptes Microsoft personnels ne peuvent pas se connecter au Web, tant qu'Entra ne les accepte pas pour la ressource Azure DevOps. | Limite de Microsoft Entra ID ; l'OAuth d'Azure DevOps qui les acceptait est déprécié. |
| Une organisation Azure DevOps non reliée à un annuaire Microsoft Entra ID (adossée à des comptes Microsoft) ne peut pas être admise tant qu'elle n'est pas reliée. | Le principal de service de l'application de l'instance n'existe que dans un tenant Entra relié à l'organisation. |
| Les statuts portent `genre=codecov` et un `name` : la stratégie de branche exige `codecov/patch`, le même nom que sur GitHub, saisi en deux parties. | Forme du statut de pull request d'Azure DevOps. |
| Le commentaire est le seul commentaire d'un fil de pull request, adressé par l'identifiant du fil. | Azure DevOps n'a pas de commentaire de premier niveau hors d'un fil. |
| Un fichier est binaire selon la règle de git (octet NUL dans les 8 000 premiers octets), sans lire `.gitattributes`. | Le diff est calculé à partir des blobs, sans les attributs du dépôt. À confirmer par la mesure de M7.01. |
| Un renommage suit l'appariement d'Azure DevOps : un renommage rapporté comme suppression plus ajout donne un ajout du fichier entier, là où `git diff -M` ne donnerait que les lignes modifiées. | L'API fournit les paires ; CodeCoverage ne les recalcule pas. À confirmer par la mesure de M7.01. |
| Au-delà de 1 Mio par côté, le patch d'un fichier est inconnu. | Plafond de coût des blobs ; la valeur est à confirmer par le coût mesuré (TSTU). |
| Couverture du patch calculée à partir de `2N` lectures de blobs au plus pour `N` fichiers, contre une lecture par 100 fichiers sur GitHub. | L'API de diff ne renvoie pas de lignes. Pas un écart de résultat, mais une limite de débit plus vite atteinte. |

ADR-013 nommait un écart sur les logins réservés : il disparaît avec M3.43a.

## Prouvé et probable

**Prouvé** à partir de la documentation officielle et du dépôt :

- Codecov liste [GitHub, GitLab et Bitbucket](https://docs.codecov.com/docs/frequently-asked-questions) ; [Azure Pipelines y figure comme CI](https://docs.codecov.com/docs/supported-ci-providers). Aucune page officielle consultée ne cite Azure Repos, sans pour autant l'exclure explicitement.
- Azure DevOps : [règles de nommage](https://learn.microsoft.com/azure/devops/organizations/settings/naming-restrictions) (organisation sous 50 caractères, en lettres, chiffres et tirets ; `:` interdit dans un projet, espaces et points admis), [statuts de pull request](https://learn.microsoft.com/azure/devops/repos/git/create-pr-status-server), [service hooks](https://learn.microsoft.com/azure/devops/service-hooks/services/webhooks), authentification recommandée par Microsoft Entra ID.
- Bitbucket Cloud : [statuts de build par commit](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-commit-statuses/), [webhooks signés par HMAC](https://support.atlassian.com/bitbucket-cloud/docs/manage-webhooks/), [OIDC dans Pipelines](https://support.atlassian.com/bitbucket-cloud/docs/deploy-on-aws-using-bitbucket-pipelines-openid-connect/).
- GitLab : [règles des chemins](https://docs.gitlab.com/user/reserved_names/), [255 caractères et 20 niveaux](https://gitlab.com/gitlab-org/gitlab/-/blob/master/app/models/namespace.rb), [`compare`](https://docs.gitlab.com/api/repositories/), [statuts](https://docs.gitlab.com/api/commits/), [notes](https://docs.gitlab.com/api/notes/), [webhooks](https://docs.gitlab.com/user/project/integrations/webhooks/), jetons [de groupe](https://docs.gitlab.com/user/group/settings/group_access_tokens/) et [de projet](https://docs.gitlab.com/user/project/settings/project_access_tokens/) réservés à Premium, [ID tokens](https://docs.gitlab.com/ci/secrets/id_token_authentication/).
- Codecov encode un slug à sous-groupes en [`group:::sub::::repo`](https://github.com/codecov/codecov-cli/blob/main/codecov_cli/helpers/encoder.py).
- Le dépôt : aucune des routes, tables ou ports visés n'est construit, hormis `Organizations` (M1.16b).

**Prouvé** pour l'amendement M7.02 (documentation officielle consultée le 2026-11-16, et spike M7.01) :

- [L'OAuth d'Azure DevOps est déprécié](https://learn.microsoft.com/azure/devops/integrate/get-started/authentication/azure-devops-oauth) : plus de nouvelle application depuis avril 2025, retrait prévu en 2026 ; Microsoft recommande [Entra ID OAuth](https://learn.microsoft.com/azure/devops/integrate/get-started/authentication/entra-oauth).
- [Les applications Entra ne prennent pas en charge les comptes Microsoft personnels pour la ressource Azure DevOps](https://learn.microsoft.com/azure/devops/integrate/get-started/authentication/entra-oauth#tips-for-building-and-migrating) ; Microsoft y travaille, sans date publiée.
- [Un principal de service](https://learn.microsoft.com/azure/devops/integrate/get-started/authentication/service-principal-managed-identity) doit exister dans le tenant relié à l'organisation, y être ajouté explicitement comme utilisateur, et Azure Repos exige l'accès Basic ou plus ; un accès payant est facturé dans chaque organisation, et les mêmes limites de débit que pour un utilisateur s'appliquent. Une application multi-tenant a un principal de service dans chaque tenant qui y consent.
- [Une organisation Azure DevOps est reliée à un annuaire Entra ou non](https://learn.microsoft.com/azure/devops/organizations/accounts/access-with-azure-ad) : non reliée, elle sert surtout des comptes Microsoft personnels ; reliée, elle n'admet que les membres et invités du tenant. [Un principal de service](https://learn.microsoft.com/azure/devops/integrate/get-started/authentication/service-principal-managed-identity) exige une organisation reliée à un tenant Entra. [Un tenant Entra ID Free](https://learn.microsoft.com/entra/fundamentals/create-new-tenant) s'obtient par un compte Azure gratuit, et son créateur en devient administrateur général. [La page **Microsoft Entra ID** des réglages de l'organisation](https://learn.microsoft.com/azure/devops/organizations/accounts/faq-azure-access) indique l'état (**Connect directory** ou **Disconnect directory**). [La liaison](https://learn.microsoft.com/azure/devops/organizations/accounts/connect-organization-to-azure-ad) exige d'être membre de « Project Collection Administrators » et membre du tenant de destination, et, au-delà de 100 utilisateurs, le support rattache les utilisateurs déconnectés.
- [Les service hooks Web Hooks](https://learn.microsoft.com/azure/devops/service-hooks/consumers#webhooks) acceptent des en-têtes HTTP (`httpHeaders`) et des identifiants Basic, sans signature ; [`Minimal`](https://learn.microsoft.com/azure/devops/service-hooks/services/webhooks#resource-details-to-send) n'envoie que les champs clés, l'appelant devant relire la ressource avec ses propres droits ; [`resourceVersion`](https://learn.microsoft.com/azure/devops/service-hooks/create-subscription#resource-versions-advanced) doit être explicite pour une charge utile stable ; créer un webhook exige d'être administrateur du projet ou de la collection.
- [Le statut de pull request](https://learn.microsoft.com/azure/devops/repos/git/pull-request-status) porte un contexte `genre` et `name` ; une stratégie de branche exige `genre/name`.
- `Diffs - Get` ne renvoie aucune ligne modifiée, seulement des changements par fichier (spike M7.01, §3.1, prouvé par la documentation).
- Le portage de xdiff reproduit `git diff --no-index -U0` sur 300 éditions aléatoires sur 300 (spike M7.01, §3.3, prouvé localement).

**Probable**, non exécuté à la date de cette décision :

- [L'API de diff d'Azure DevOps](https://learn.microsoft.com/javascript/api/azure-devops-extension-api/gitcommitdiffs) renvoie des changements par fichier (`GitChange[]`), sans lignes modifiées. Ce point, probable en 2026-11-08, est **prouvé par la documentation depuis le spike M7.01** ; les lignes sont calculées à partir des deux versions de chaque fichier (voie C).
- **Amendement M7.02, encore probable** et à établir par l'exécution de la mesure de M7.01 sur au moins dix pull requests réelles (`tools/azure-devops-diff`) :
  - la voie C reproduit `git diff -U0` sur des données Azure DevOps (au moins neuf pull requests sur dix), ce qui suppose que `Diffs - Get` donne les mêmes fichiers, identifiants d'objet et renommages que git ;
  - un fil créé par l'application ne contient qu'un commentaire, d'identifiant 1 ;
  - `instanceId` de `_apis/connectionData` est un GUID stable de l'organisation, lisible par le principal de service ;
  - la liste des pull requests actives filtrée sur `lastMergeSourceCommit` trouve la pull request dont un commit est la tête ;
  - l'appartenance à « Project Collection Administrators » est lisible avec le jeton de l'application (M7.54) ; seule l'appartenance directe est vérifiée, un groupe imbriqué exige `direction=up` ;
  - le coût en TSTU d'une pull request, donc le plafond de 1 Mio.
- Bitbucket Cloud expose un diff unifié et des UUID de workspace : non confirmé par nos recherches, à vérifier au début de M8.
- Les badges Codecov de sous-groupes GitLab s'écrivent `/gl/group:sub/repo` : seul un [ancien fil communautaire](https://community.codecov.com/t/cannot-add-new-gitlab-repository/1342) l'atteste. **Le spike de M9 le vérifie.**
- `ExternalAccountId` vaut l'identifiant de namespace, aussi bien pour un groupe que pour un espace personnel, et un espace personnel n'a pas de sous-groupes : la documentation consultée ne le dit pas explicitement.
- La forme de l'OIDC d'Azure Pipelines ne convient pas à un service tiers : le jeton s'obtient par `System.AccessToken` et sert d'abord aux fédérations Entra.

## Risques résiduels

- Le calcul des lignes est nécessaire sur Azure DevOps : le résultat peut différer de celui de `git diff` sur des cas limites (renommages, binaires déclarés par `.gitattributes`), et donc la couverture du patch. La mesure de M7.01 borne ce risque avant M7.46.
- **Amendement M7.02 :**
  - les valeurs des en-têtes d'un abonnement de service hook sont lisibles par quiconque accède à l'abonnement dans Azure DevOps. Le secret est propre à une connexion, tourne (M7.13), et une livraison forgée ne fait que déclencher une relecture par l'API ;
  - une organisation dont l'administrateur retire l'accès Basic ou le consentement de l'application cesse d'être lue : la santé des connexions (M7.14) la signale en échec le lendemain ;
  - le retrait de l'OAuth d'Azure DevOps ne touche pas l'instance, qui ne l'emploie pas ; la prise en charge future des comptes personnels par Entra rouvrira l'écart correspondant ;
  - le coût en TSTU de la voie C peut rendre la limite de 200 TSTU par identité et par fenêtre de cinq minutes plus proche sur de grosses pull requests : un cache par identifiant de blob (immuable) l'atténue, et la limitation de débit neutre (M7.03) replanifie le job.
- Sur le plan Free de GitLab.com, ni jeton de groupe, ni jeton de projet, ni webhook de groupe : l'accès passerait par un compte dédié ou OAuth, et un webhook par projet. **Le spike de M9 tranche.**
- GitLab peut omettre des diffs volumineux : la couverture du patch reste alors inconnue pour ces fichiers.
- Un sous-groupe déplacé vers un autre groupe de premier niveau change d'organisation : la règle de transfert d'ADR-013 s'applique, sans déplacement de données.
- Une abstraction fondée sur un seul adaptateur peut se révéler mal dessinée : `IForgeGateway` sera probablement corrigé par M7.
- Une personne présente sur plusieurs forges a plusieurs comptes et plusieurs sessions.
- Les maquettes de M3.18 décrivent des adresses sans forge jusqu'à leur mise à jour.

## Conséquences

- **Positives :**
  - la porte d'entrée, les adresses et le schéma des dépôts se construisent une seule fois ;
  - ajouter une forge revient à écrire un adaptateur et ses connexions, sans migration d'adresse ;
  - un écart non configurable avec Codecov disparaît.
- **Négatives :**
  - des adresses un peu plus longues pour un produit qui ne sert d'abord que GitHub ;
  - une préparation d'environ 3 à 4 sessions, réparties sur les fiches concernées ;
  - `docs/ARCHITECTURE.md` et `AGENT.md` ont décrit GitHub seul jusqu'à M1.16a2.
- **Conséquences de l'amendement M7.02 :**
  - M7, M8 et M9 sont planifiés en micro-features (`docs/PLAN.md` §9.1 à §9.3), plus de 150 au total ; M7 commence par les refactors neutres, chacun sans régression de GitHub, avant l'adaptateur Azure DevOps ;
  - chaque organisation Azure DevOps admise paie un accès Basic pour l'application de l'instance, et un administrateur de projet doit accorder le droit de créer les service hooks ;
  - le Web accueille un deuxième fournisseur de connexion (Entra ID) et un paquet Microsoft d'OpenID Connect (M7.53) ;
  - M7.46 attend la mesure de M7.01 par le propriétaire.

## Alternatives écartées

- **Rester sur GitHub :** les autres organisations ne seraient pas servies, et les adresses devraient être migrées plus tard.
- **Ajouter la seule colonne `Forge` :** les adresses restent ambiguës dès qu'un même owner existe sur deux forges.
- **Préfixer les seules forges non GitHub :** deux familles de routes, et `ReservedLogins` à conserver.
- **Une famille de routes à quatre segments pour Azure DevOps :** toutes les routes de dépôt seraient doublées.
- **Traiter le projet Azure DevOps comme tenant :** les jetons et réglages d'organisation seraient répétés par projet.
- **Un identifiant de forge dans le `long` existant :** les GUID et UUID n'y entrent pas.
- **Abstraire dès maintenant webhooks et connexions :** une abstraction sans second cas réel se dessine à l'aveugle.
- **`%2F` pour les sous-groupes :** décodé différemment selon les proxys.
- **Un segment `{**owner}` :** ambigu avec les suffixes comme `/commits`.
- **Un sous-groupe GitLab comme tenant :** les jetons et réglages seraient répétés, et un projet changerait de tenant à chaque déplacement.
- **Une instance par forge :** n'évite aucun adaptateur, et isole des utilisateurs qui travaillent sur plusieurs forges.
- **Préparer sans implémenter les autres forges** (décision initiale) : remplacée par le propriétaire le 2026-11-14 (amendement M7.02).
- **OAuth d'Azure DevOps pour la connexion Web :** déprécié, fermé aux nouvelles applications et retiré en 2026.
- **Un jeton d'accès personnel (PAT) par organisation Azure DevOps :** lié à une personne et à son départ, d'une durée limitée, et de plus en plus restreint par les politiques des organisations ; l'application Entra n'a pas ces défauts.
- **Une application Entra par organisation cliente :** chaque client devrait créer et faire tourner ses propres secrets ; une application multi-tenant de l'instance suffit, le consentement créant le principal de service dans chaque tenant.
- **`resourceDetailsToSend=All` et confiance dans le corps du service hook :** un secret d'en-tête volé suffirait à injecter un état ; la relecture par l'API passe par les droits de l'application.
- **`FileDiffs` comme source du diff :** préversion sans texte de ligne, algorithme non documenté (spike M7.01).
- **Un diff de Myers simple au lieu du portage de xdiff :** 53 cas sur 60 seulement identiques à git (spike M7.01).
