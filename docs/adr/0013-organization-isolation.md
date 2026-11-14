# ADR-013 : Isolation entre organisations sur une instance partagée

- **Statut :** accepté le 2026-11-09 (M1.16a2), M1.16d2 et le jeton de concurrence étant verts sur PostgreSQL et SQL Server. Remplace [ADR-003](0003-single-tenant.md).
- **Date :** 2026-11-08
- **Références :** `docs/PLAN.md` §1.4, §4.4 (M1.16a1 à M1.16i), §10 et §11 ; `docs/ARCHITECTURE.md` §1.3, §3bis, §4.2, §5.1, §6.1, §8, §10.2 et §10bis, réécrits par M1.16a2 ; [ADR-014](0014-multi-forge.md) complète l'identité et les adresses

## Contexte

- ADR-003 retenait une organisation GitHub par déploiement, avec une isolation par dépôt seulement.
- Le 2026-11-08, il a été décidé qu'**une seule instance héberge plusieurs organisations**, avec une isolation réelle : une organisation ne doit pouvoir ni lire, ni modifier, ni deviner les données d'une autre.
- Aucune table métier n'existe encore : les migrations ne contiennent que `Jobs`, `AuditEntries`, `Settings`, `SettingsRevision` et `DataProtectionKeys`, et M1.17 ne dépend que de M1.03 et M0.15. C'est le moment le moins coûteux pour introduire l'organisation.
- La règle critique 1 d'`AGENT.md` exige que chaque écart avec Codecov soit nommé, documenté **et configurable**. Certains écarts protègent l'isolation elle-même : les rendre configurables créerait des interrupteurs qui l'affaiblissent.

## Décision

### Arbitrages du propriétaire

| # | Question | Décision |
|---|---|---|
| 1 | Une GitHub App par organisation, ou une App partagée ? | **Une App publique partagée**, installable par tout compte, inerte tant que le compte n'est pas admis. |
| 2 | Règle 1 : écarts de multi-organisation configurables ? | **Non.** La règle 1 est amendée (M1.16a2) : ces écarts sont nommés et documentés ici, sans être configurables. |
| 3 | L'administrateur d'instance lit-il les données d'une organisation ? | **Non.** Un accès de support explicite, limité dans le temps et audité pourra être ajouté si le besoin apparaît. |
| 4 | L'historique suit-il un dépôt transféré ? | **Non.** Il repart de zéro chez le destinataire et reste chez l'origine ; un retour réactive le dépôt sans ses anciens jetons. |
| 5 | Écrans de jobs, webhooks et audit pour les organisations ? | **Pas dans la première livraison.** Les journaux d'upload restent visibles sur la page du commit. |
| 6 | Effacement des données avant le pilote ? | **Non.** La suspension couvre le départ d'une organisation ; l'effacement est au backlog (PLAN §11). |
| 7 | Exposition du Web (PLAN §11-6) ? | **Public, avec connexion GitHub**, puisque des utilisateurs de plusieurs organisations doivent y accéder. |

### Identité et admission

- Une organisation est **un compte GitHub**, comptes personnels compris, identifié par son identifiant numérique. La table globale `Organizations` porte `GitHubAccountId` (unique), `Login`, `NormalizedLogin` (unique), `AccountType`, `State` (`Pending`, `Active`, `Suspended`), `SuspensionReason`, `AdmittedBy` et `Version`.
- Un login repris par un autre compte est remplacé sur l'ancienne ligne par une pierre tombale (`~stale-{accountId}`, qui n'est pas un login GitHub valide), et une réconciliation est mise en file.
- L'admission est **fermée** : `installation.created` crée une organisation `Pending` qui ne suit rien, et un administrateur d'instance l'admet. Avant M2, le verbe `admin admit-organization` crée directement une organisation `Active`. Une réinstallation par le même compte le rattache.
- L'App demande `Organization Members: read` dès son premier manifest et s'abonne à `organization`, `member`, `membership` et `installation_target`. Les secrets `GitHub:*` restent au niveau de l'instance.

### Mécanisme d'isolation

Quatre mécanismes tiennent l'isolation par construction.

1. **Filtre EF sur les tables d'organisation.** Chaque table d'organisation porte une colonne fantôme `OrganizationId` non nulle, marquée par `IsOrganizationOwned`, et un unique filtre nommé `Organization` installé par une boucle dans `OnModelCreating`. Le filtre lit `ICurrentOrganization`, dont l'accesseur lève si le contexte n'est pas posé. Il n'y a **aucune clause pour le principal système**.
2. **Aucun contournement.** `IgnoreQueryFilters` est interdit dans `src`, avec une liste d'exceptions vide. Ce qui précède l'entrée dans une organisation ne lit que des tables globales sans donnée métier : owner dans `Organizations`, `installation.id` dans `Installations`, hash de jeton API dans `ApiTokens`, utilisateur dans `OrganizationMemberships`. Le travail inter-organisations est un job coordinateur qui lit `Organizations` et met en file un enfant par organisation.
3. **Une seule porte.** Une portée DI n'entre dans une organisation que par `AuthorizationBehavior` (à partir du login de l'owner, à chaque envoi, avant les validateurs), par `JobWorker` (à partir de la ligne `Jobs`) ou par `SystemCommandRunner` (CLI d'administration). L'entrée n'a lieu qu'une fois par portée. Chaque message déclare exactement un marqueur d'accès et sert une seule audience ; `IOrganizationScoped.RequiredRole` n'a pas de valeur par défaut. La mise en file pour une autre organisation par identifiant est limitée à une liste de sources nommées. La règle de build réserve `IOrganizationScope` à `AuthorizationBehavior` ; `JobWorker` emploie directement `OrganizationContext.Enter`, que cette règle ne couvre pas (voir « Risques résiduels »), et `SystemCommandRunner` n'existe pas encore (M1.22a).
4. **Écritures, adresses et clés.** `SaveChanges` estampille les lignes ajoutées et lève sur une ligne modifiée ou supprimée d'une autre organisation. La colonne `OrganizationId` est aussi un jeton de concurrence, posé une seule fois dans `IsOrganizationOwned` : chaque `UPDATE` et chaque `DELETE` d'une sauvegarde la porte dans son `WHERE`, à côté de la clé primaire. Toute adresse routée contient l'owner. `SqlJobQueue` préfixe `LockKey` et `DedupKey` par l'organisation.

### Classement des 32 tables

Le classement est vérifié par un test : une table nouvelle non classée fait échouer le build.

| Classe | Tables |
|---|---|
| **D'organisation, filtrées (23)** | `Repositories`, `RepositoryFlags`, `Branches`, `GitCommits`, `Commits`, `CommitFlagTotals`, `PullRequests`, `CommitConfigs`, `DiffCache`, `RepositoryTrees`, `Uploads`, `CommitSessions`, `CommitReports`, `Comparisons`, `PostedStatuses`, `UploadRawFiles`, `UploadRawChunks`, `UploadCoverages`, `UploadTokens`, `UserRepositoryAccess`, `OrganizationSettings`, `CodecovImports` (à partir de M5.06), `CarryforwardWaits` (#165) |
| **Opérationnelles, `OrganizationId` nullable, sans filtre (3)** | `Jobs`, `AuditEntries`, `WebhookDeliveries` |
| **Globales, sans donnée métier (8)** | `Organizations`, `Installations`, `OrganizationMemberships`, `Users`, `ApiTokens`, `Settings`, `SettingsRevision`, `DataProtectionKeys` |

Seules `Repositories`, `UploadTokens`, `OrganizationSettings` et `UserRepositoryAccess`, dont l'index unique commence par `OrganizationId`, ont une clé étrangère vers `Organizations`. Les clés étrangères composites ne sont pas utilisées.

### Refus uniformes

Avant qu'un credential soit prouvé, la réponse ne dépend jamais de l'état de l'organisation.

- Upload, dans l'ordre : valider un JWT s'il y en a un ; comparer son dépôt au slug (403 `Upload.OidcMismatch`) ; si la porte n'a pas été franchie, un jeton reçoit 401 `Upload.TokenInvalid` et un JWT valide 404 `Repository.NotFound` ; vérifier `repository_owner_id` ; chercher le jeton sous le filtre (401 s'il est absent).
- API de gestion : `IRepositoryScoped` répond `Repository.NotFound` pour un owner inconnu, une organisation non active ou un dépôt illisible ; `IOrganizationScoped` répond `Organization.NotFound` dans les cas équivalents.
- Écriture : un `UPDATE` ou un `DELETE` d'une table d'organisation qui n'affecte aucune ligne donne 409 `General.ConcurrentChange` (`ConflictException`, rejouée par le worker), avec le même message pour une ligne supprimée entre-temps, une clé inconnue et une clé d'une autre organisation, que la base ne distingue pas. `Organizations`, table globale, garde `DbUpdateConcurrencyException` pour la reprise de son upsert.

### Résolution, GitHub et adresses

- L'owner se résout en organisation `Active`, puis le nom se résout dedans : `UX(OrganizationId, NormalizedName)` et `UX(OrganizationId, GitHubId)`. `Repositories.Owner` n'est plus qu'une copie d'affichage.
- Un dépôt suivi s'adresse sur GitHub par `/repositories/{GitHubId}/…`. Seul le suivi automatique d'un dépôt inconnu emploie owner et nom, et M1.31 exige alors que le compte de l'installation soit celui de l'organisation.
- `GET /api/v1/uploads/{id}` devient `/api/v1/repos/{owner}/{repo}/uploads/{id}`. Les jetons d'upload sont sous `/api/v1/orgs/{org}/upload-tokens` et `/api/v1/repos/{owner}/{repo}/upload-tokens`, et la liste des dépôts sous `/api/v1/orgs/{org}/repos`. Les pages d'organisation sont sous `/orgs/{org}`. `/api/v1/ops` reste au niveau de l'instance et ne renvoie que des métadonnées.
- Les logins réservés couvrent le premier segment littéral de chaque route, ce qu'un test vérifie, à l'admission comme au renommage : un renommage vers un login réservé suspend l'organisation.

### Rôles, jetons et paramètres

- **Administrateur d'instance :** `Access:AdminUserIds` et `CodeCoverage__Bootstrap__AdminUserIds`, en identifiants GitHub numériques, réévalués à chaque lecture. `IAdminOnly` n'entre dans aucune organisation : une lecture filtrée y lève.
- **`OrganizationMemberships` :** `Participant` (accès à au moins un dépôt de l'installation, aucun droit sur l'organisation) ou `Admin` (membre actif avec le rôle admin, ou propriétaire du compte personnel). Une ligne au-delà de `Access:HardExpiryMinutes` n'accorde rien.
- **Jetons d'upload :** `UX(OrganizationId, Hash)`, car une valeur importée est choisie par l'appelant. Six messages remplacent les trois initiaux, un par cas d'usage et par audience.
- **Jetons API :** un jeton appartient à exactement une organisation, choisie à la création, ou est réservé à l'instance.
- **Paramètres :** le catalogue passe à 41 clés dont 4 par organisation (`Coverage:OrganizationYaml`, `Notifications:StatusContextPrefix`, `Notifications:StatusesEnabled`, `Notifications:CommentsEnabled`), lues par un provider à portée. `GitHub:Organization` et `Uploads:AllowedOwners` disparaissent.

### Jobs et transfert

- `Jobs.OrganizationId` est nullable. `JobRequest.Create` prend l'organisation de la portée et lève sans elle ; `ForInstance` et `ForOrganization` sont explicites. `KeyMaxLength` passe à 178 pour que la clé préfixée tienne dans la colonne de 200 caractères. Le worker entre à partir de la colonne, jamais du payload, et termine un job d'une organisation non active.
- Un transfert **ne déplace aucune donnée** : le destinataire reçoit une nouvelle ligne et une réconciliation désactive l'ancienne, révoque ses jetons de dépôt, fait tourner son jeton de badge, marque `RotationAdvisedAt` sur les jetons d'organisation de l'origine et audite ces trois actions. La ligne est fermée une seule fois, ce que note `Repositories.TransferredAt` : « inactive » ne veut pas dire « fermée », car l'installation de l'origine qui cesse de servir le dépôt désactive la ligne sans toucher à un jeton, et peut le faire avant que le transfert ne soit connu.
- `ImportCodecovHistory` s'exécute en job ; le credential Codecov est chiffré dans `CodecovImports`, référencé par identifiant et supprimé en fin d'import.

## Écarts avec Codecov

| Écart | Configurable |
|---|---|
| L'App doit être installée et l'organisation admise. Avant cela, un upload par jeton reçoit 401 `Upload.TokenInvalid` et un upload OIDC valide `Repository.NotFound` ; l'uploader sort quand même en 0 (règle 6). | **Non**, par conception |
| Le jeton d'upload global de Codecov n'existe pas : un jeton ne vaut que dans l'organisation qui l'a créé ou importé. Les `CODECOV_TOKEN` de dépôt et d'organisation importés continuent de fonctionner. | **Non**, par conception |
| Un dépôt transféré repart avec un historique vide, sans jeton d'upload et avec un nouveau jeton de badge ; l'ancien historique reste à l'organisation d'origine. | **Non**, par conception |
| Après un renommage de compte GitHub, l'ancien owner ne résout plus : les badges affichent « unknown » et les uploads doivent utiliser le nouveau slug. Aucune redirection n'est conservée, car un login repris ne doit jamais atteindre l'ancienne organisation. | **Non**, par conception |
| Une même valeur de jeton importée peut exister dans deux organisations, chacune valide sous son seul owner. | Non : conséquence de l'isolation |
| L'App demande `Organization Members: read`, visible par l'organisation à l'installation. | Non : requis pour les rôles |
| La confiance OIDC vient de l'identifiant numérique du compte admis (`repository_owner_id`) et de `repository_id`, à la place de la liste `Uploads:AllowedOwners`. | Non : remplace une liste configurable |
| Le préfixe de statut par défaut reste `coverage`, et chaque organisation passe `Notifications:StatusContextPrefix` à `codecov` à sa propre date ; les noms de statut sont alors ceux de Codecov. | **Oui**, par organisation |

L'écart des logins réservés (un login égal à un segment de route du produit ne pouvait pas être admis) a disparu avec M3.43a : l'owner n'est plus le premier segment d'une route ([ADR-014](0014-multi-forge.md)).

## Prouvé et probable

**Prouvé** à partir du dépôt, sans exécution du futur code :

- `AuthorizationBehavior` laisse passer un message sans marqueur, et le marqueur est lu par type de message ; `AuthorizationBehavior` précède la validation (`PipelineOrderTests`), la porte reçoit donc une entrée brute.
- `SqlJobQueue` n'a qu'un chemin de mise en file (`EnqueueInTransactionAsync`), sa recherche de jumeau porte sur `DedupKey` et l'état seulement, et `JobRequest.KeyMaxLength` vaut la longueur de la colonne (200).
- `ReadCommittedSnapshotHealthCheck` emploie `SqlQuery` : elle entre dans la liste d'exceptions du SQL brut.
- `CircuitUserScopeInitializer` copie `IsAdmin` pour la durée du circuit ; `CurrentUserContext.SetSystem` est `internal`, inaccessible depuis les hôtes.
- `Architecture.Tests` référence déjà Roslyn ; `SettingsOptionsBindingTests` n'emploie que des clés qui restent au niveau de l'instance.
- Une valeur de niveau instance pour une clé `Organization` ne serait modifiable nulle part, ce que la règle 7 interdit : la précédence est donc la ligne de l'organisation, puis le défaut du catalogue.

**Prouvé par exécution** sur PostgreSQL 17 et SQL Server 2022 (M1.16d1, M1.16d2 et le jeton de concurrence, `Infrastructure.Tests/Persistence/Organizations`) :

- Le filtre nommé couvre les requêtes, `ExecuteUpdate` et `ExecuteDelete`, et lève sans organisation ; chaque contexte lit sa propre organisation bien que le modèle soit construit une seule fois (`OrganizationFilterTests`).
- L'index unique qui commence par `OrganizationId` sert la clé étrangère : la convention n'en crée pas d'autre (`ModelClassificationTests`).
- `SaveChanges` et `SaveChangesAsync` estampillent une ligne ajoutée et lèvent sans organisation. La colonne fantôme comprise dans l'index unique reçoit bien la valeur estampillée : une même clé coexiste dans deux organisations, un doublon dans une organisation est refusé par la base, et une clé supprimée puis ajoutée dans une même sauvegarde passe (`OrganizationWriteGuardTests`).
- La garde lève, sans rien écrire, sur une ligne modifiée ou supprimée d'une autre organisation, sur une ligne déplacée vers une autre organisation et sur une ligne ajoutée au nom d'une autre (`OrganizationWriteGuardTests`). Sauvegarder les seules tables globales et opérationnelles ne demande pas d'organisation.
- La colonne fantôme ne voyage pas avec une instance : `Update` ou `Remove` d'une instance détachée la laisse à zéro et la garde refuse la ligne. Une ligne d'organisation doit donc être lue par le contexte qui l'écrit, ce qui s'impose aux repositories à partir de M1.17.
- Une instance dont la colonne fantôme reçoit à la main l'organisation courante passe la garde, que la valeur soit écrite avant l'attachement ou après, avec ou sans sa valeur d'origine. Sans le jeton de concurrence, `Remove` supprime la ligne de l'autre organisation et `Update` la réécrit : les 18 cas d'attaque échouent sur les deux fournisseurs une fois le jeton retiré. Avec le jeton, le `WHERE` porte l'organisation courante, aucune ligne n'est touchée et la ligne de l'autre organisation reste intacte en base (`OrganizationConcurrencyTokenTests`). Le refus est identique pour une clé d'une autre organisation et pour une clé inconnue, et un échec n'écrit rien, y compris les lignes ajoutées dans la même sauvegarde.
- Les deux migrations `AddOrganizationConcurrencyToken` sont vides : le jeton ne change que l'instantané du modèle. Une ligne lue par le contexte se modifie et se supprime comme avant, et `ExecuteUpdate` et `ExecuteDelete` ne sont pas affectés (`OrganizationWriteGuardTests`, `OrganizationFilterTests`).

**Rejoué le 2026-11-09 pour passer à « accepté »**, avec Docker : les 272 tests d'`Infrastructure.Tests` portant sur `Persistence/Organizations`, `Identity`, la file de jobs, `OrganizationSettingsProviderTests` et `RepositoryIsolationCoverageTests` sont verts sur les deux fournisseurs, aucun n'étant ignoré. Les 129 tests d'`Architecture.Tests` et les 37 tests d'`AuthorizationBehaviorTests` et `PipelineOrderTests` sont verts.

**Probable**, non exécuté à la date de cette décision :

- Les comportements GitHub : SAML et `GET /user/installations`, collaborateurs externes dans cette liste, contenu de l'événement `transferred`, réception de `installation_repositories` par l'origine, `installation_target` sur les deux types de compte, secrets Actions après un transfert, lecture d'un dépôt public d'un autre compte par un jeton d'installation, forme `/repositories/{id}` sur chaque endpoint. **M4.02a les vérifie sur deux comptes réels avant M4.08** ; le pilote M1.47 exerce la forme par identifiant plus tôt.

## Risques résiduels

- Un owner actif coûte une lecture de plus qu'un owner inconnu sur le chemin d'upload : une différence de temps reste possible, exploitable seulement statistiquement et ne révélant que l'existence de l'organisation.
- Entre un transfert et la réconciliation de l'origine, un jeton de dépôt resté dans les secrets du dépôt transféré peut encore écrire sur l'ancienne ligne. La fenêtre est bornée par l'événement du destinataire, ou par la synchronisation quotidienne s'il est perdu.
- Un jeton d'organisation présent dans les secrets d'un dépôt transféré reste valide jusqu'à sa rotation manuelle ; la conception ne pose qu'un bandeau audité (`RotationAdvisedAt`), car une révocation aveugle casserait les autres dépôts de l'origine.
- Sans clé étrangère composite, un bogue écrivant l'identifiant d'un parent d'une autre organisation n'est pas refusé par la base. La ligne reste dans l'organisation qui écrit et se lit vide sous le filtre.
- Qui exploite la base, les sauvegardes et les journaux voit toutes les organisations ; `docs/ARCHITECTURE.md` §12.5 et §12.6 le disent. La console d'instance n'expose aucune donnée métier.
- Le retard de jobs d'une organisation peut retarder les statuts d'une autre tant que la réclamation équitable n'existe pas ; seule une mesure pendant le pilote dira si cela compte.
- Les données d'une organisation suspendue sont conservées sans limite tant que l'effacement n'est pas construit, ce qui peut compter face à une demande légale.
- Les administrateurs d'organisation ne voient ni leurs échecs de job ni leur audit, et l'administrateur d'instance ne voit que des métadonnées : diagnostiquer un job échoué peut demander un accès opérateur aux journaux.
- La règle de build de la seule porte ne couvre que `IOrganizationScope`, réservé à `AuthorizationBehavior`. `OrganizationContext.Enter` est `internal`, ce qui le tient hors de l'Application et des hôtes, mais aucune règle ne limite ses appelants dans l'Infrastructure : `OrganizationScope` et `JobWorker` sont les seuls à ce jour, et seule la revue garde cet état. L'ajout d'une règle reste à décider.

## Conséquences

- **Positives :**
  - l'isolation tient par défaut et ne repose pas sur une vingtaine de repositories écrits à la main ; les tests d'architecture font échouer le build en cas de contournement ;
  - les entités du Domain et les signatures des ports restent inchangées ;
  - la colonne `OrganizationId` permet d'ajouter plus tard, sans reprise de données, des clés composites ou de la sécurité au niveau des lignes.
- **Négatives :**
  - 21 micro-features nouvelles et 58 modifiées, soit environ 48 sessions estimées ;
  - M1.17 attend M1.16d2 et M1.16f ;
  - `/api/v1/uploads/{id}` et `/api/v1/repos` sont remplacées par des routes portant l'owner ou l'organisation ;
  - `docs/ARCHITECTURE.md` et `AGENT.md` ont décrit une organisation unique jusqu'à M1.16a2, qui attendait la preuve de M1.16d2.

## Alternatives écartées

- **Une App par organisation :** tous les `GitHub:*` passent par organisation, avec un routage OAuth et webhook par organisation (8 à 12 sessions de plus).
- **`OrganizationId` sur `Repositories` seulement :** une jointure oubliée expose un rapport entier.
- **Paramètre explicite sur chaque port :** la confiance se déplace vers une vingtaine de repositories écrits à la main.
- **Sécurité au niveau des lignes :** SQL propre à un fournisseur à l'exécution et second rôle de base ; reste possible plus tard avec la même colonne.
- **Schéma ou base par organisation :** les migrations se multiplient par le nombre d'organisations.
- **Filtre sur `Jobs` :** la réclamation des jobs n'est délibérément pas composée, et la maintenance de la file s'exécute sans organisation.
- **Clés étrangères composites :** environ 15 index, pour un écart qui exige déjà un bogue et un identifiant étranger.
- **Copier l'organisation dans `CircuitUserScopeInitializer` :** cette copie vivrait aussi longtemps que l'onglet.
- **Limiteur par adresse IP avant authentification :** ne traite pas la fuite, corrigée par les refus uniformes, et brime les adresses partagées des agents CI.
