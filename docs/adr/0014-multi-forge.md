# ADR-014 : Préparation à plusieurs forges

- **Statut :** accepté le 2026-11-09 (M1.16a2). Complète [ADR-013](0013-organization-isolation.md).
- **Date :** 2026-11-08
- **Références :** `docs/PLAN.md` §1.4, §2.1, §2.3, §4.4, §9, §10 et §11 ; `docs/ARCHITECTURE.md` §1.3, §4.2, §5.1, §8, §10.2 et §10bis.3, réécrits par M1.16a2

## Contexte

- Le produit ne connaît que GitHub : `ARCHITECTURE.md` §1.3 classe GitLab et Bitbucket hors périmètre, la règle 2 d'`AGENT.md` tire le diff de l'API GitHub, et ADR-013 définit une organisation comme un compte GitHub.
- Une instance doit pouvoir servir des organisations réparties sur GitHub, Azure DevOps, Bitbucket et GitLab.com.
- Rien de ce qui fige cette hypothèse n'est encore construit : `Organizations` n'est pas livrée, et ni la porte d'entrée (M1.16c), ni les dépôts (M1.17), ni le port de forge (M1.24), ni les routes ne sont écrits. C'est le moment le moins coûteux pour rendre l'identité et les adresses indépendantes de la forge.
- Le coût réel n'est pas dans une colonne, qu'une migration ajoute à tout moment, mais dans les adresses et les ports, qui ne se changent plus une fois publiés et consommés.

## Décision

**Préparer maintenant, implémenter GitHub seul.** L'identité, les adresses et le port principal deviennent indépendants de la forge. Azure DevOps, Bitbucket et GitLab.com sont des jalons ultérieurs (M7, M8 et M9), après la bascule.

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
- **Restent propres à GitHub :** `IGitHubAppGateway` pour les installations, la conversion du manifest et les Check Runs ; `GitHubWebhookEvent` et la réception des webhooks ; `IGitHubUserGateway` et `IGitHubUserTokenProvider`. `Installations` reste une table GitHub ; une table de connexions arrive avec M7, où les secrets d'accès sont portés par organisation.
- On ne construit aucune abstraction de webhooks ni de connexion tant qu'une seule forge existe : le deuxième adaptateur en donnera la bonne forme.

### Règles critiques

- **Règle 1.** La compatibilité avec Codecov vaut pour GitHub, Bitbucket et GitLab. Azure DevOps n'a pas de référence : son comportement suit celui de GitHub, et ses écarts sont nommés ci-dessous.
- **Règle 2.** Le diff vient toujours de l'API de la forge, jamais de l'historique git de l'agent de CI. La règle est reformulée par M1.16a2.

## Écarts avec Codecov

| Écart | Configurable |
|---|---|
| L'API de gestion et les pages Web portent la forge. Les URL de badge et l'upload compatibles Codecov ne changent pas. | **Non**, par conception |
| Azure DevOps n'est pas documenté par Codecov comme hébergeur de code : ses statuts, ses badges et ses commentaires n'ont pas de référence à reproduire. | Non : aucune référence |
| Sur Azure DevOps, l'upload se fait par jeton seulement. | Non, jusqu'à preuve d'un OIDC exploitable |
| Un ancien chemin GitLab commençant par `_` ou `.` ne peut pas être admis. | Non : règle de forme commune |
| Un dépôt GitLab dont les sous-groupes dépassent 512 caractères n'est pas suivi. | Non : borne de l'index |

ADR-013 nommait un écart sur les logins réservés : il disparaît avec M3.43a.

## Prouvé et probable

**Prouvé** à partir de la documentation officielle et du dépôt :

- Codecov liste [GitHub, GitLab et Bitbucket](https://docs.codecov.com/docs/frequently-asked-questions) ; [Azure Pipelines y figure comme CI](https://docs.codecov.com/docs/supported-ci-providers). Aucune page officielle consultée ne cite Azure Repos, sans pour autant l'exclure explicitement.
- Azure DevOps : [règles de nommage](https://learn.microsoft.com/azure/devops/organizations/settings/naming-restrictions) (organisation sous 50 caractères, en lettres, chiffres et tirets ; `:` interdit dans un projet, espaces et points admis), [statuts de pull request](https://learn.microsoft.com/azure/devops/repos/git/create-pr-status-server), [service hooks](https://learn.microsoft.com/azure/devops/service-hooks/services/webhooks), authentification recommandée par Microsoft Entra ID.
- Bitbucket Cloud : [statuts de build par commit](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-commit-statuses/), [webhooks signés par HMAC](https://support.atlassian.com/bitbucket-cloud/docs/manage-webhooks/), [OIDC dans Pipelines](https://support.atlassian.com/bitbucket-cloud/docs/deploy-on-aws-using-bitbucket-pipelines-openid-connect/).
- GitLab : [règles des chemins](https://docs.gitlab.com/user/reserved_names/), [255 caractères et 20 niveaux](https://gitlab.com/gitlab-org/gitlab/-/blob/master/app/models/namespace.rb), [`compare`](https://docs.gitlab.com/api/repositories/), [statuts](https://docs.gitlab.com/api/commits/), [notes](https://docs.gitlab.com/api/notes/), [webhooks](https://docs.gitlab.com/user/project/integrations/webhooks/), jetons [de groupe](https://docs.gitlab.com/user/group/settings/group_access_tokens/) et [de projet](https://docs.gitlab.com/user/project/settings/project_access_tokens/) réservés à Premium, [ID tokens](https://docs.gitlab.com/ci/secrets/id_token_authentication/).
- Codecov encode un slug à sous-groupes en [`group:::sub::::repo`](https://github.com/codecov/codecov-cli/blob/main/codecov_cli/helpers/encoder.py).
- Le dépôt : aucune des routes, tables ou ports visés n'est construit, hormis `Organizations` (M1.16b).

**Probable**, non exécuté à la date de cette décision :

- [L'API de diff d'Azure DevOps](https://learn.microsoft.com/javascript/api/azure-devops-extension-api/gitcommitdiffs) renvoie des changements par fichier (`GitChange[]`), sans lignes modifiées documentées. Il faudrait alors calculer les lignes à partir des deux versions de chaque fichier. **Un spike ouvre M7.**
- Bitbucket Cloud expose un diff unifié et des UUID de workspace : non confirmé par nos recherches, à vérifier au début de M8.
- Les badges Codecov de sous-groupes GitLab s'écrivent `/gl/group:sub/repo` : seul un [ancien fil communautaire](https://community.codecov.com/t/cannot-add-new-gitlab-repository/1342) l'atteste. **Le spike de M9 le vérifie.**
- `ExternalAccountId` vaut l'identifiant de namespace, aussi bien pour un groupe que pour un espace personnel, et un espace personnel n'a pas de sous-groupes : la documentation consultée ne le dit pas explicitement.
- La forme de l'OIDC d'Azure Pipelines ne convient pas à un service tiers : le jeton s'obtient par `System.AccessToken` et sert d'abord aux fédérations Entra.

## Risques résiduels

- Si le calcul des lignes est nécessaire sur Azure DevOps, le résultat peut différer de celui de `git diff` sur des cas limites, et donc la couverture du patch.
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
  - M7 (environ 28 sessions), M8 (environ 20 sessions) et M9 (environ 22 sessions) restent à faire. Ces ordres de grandeur ne sont pas mesurés.
  - `docs/ARCHITECTURE.md` et `AGENT.md` ont décrit GitHub seul jusqu'à M1.16a2.

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
