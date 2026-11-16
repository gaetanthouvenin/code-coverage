# ADR-017 : Connexions de forge

- **Statut :** **accepté** par le propriétaire le 2026-11-16 (M7.02), proposé le même jour. Complète [ADR-013](0013-organization-isolation.md) et [ADR-014](0014-multi-forge.md).
- **Date :** 2026-11-16
- **Références :** `docs/PLAN.md` M7.02, M7.06, M7.07, M7.10, M7.12 à M7.14, M7.21, M7.23, M7.49, M8 et M9 ; `docs/ARCHITECTURE.md` §4.2, §6.1 et §8 ; `TableClassification`, `OrganizationIsolationTests`

> **Numéro.** `docs/PLAN.md` nommait cette décision « ADR-015 » quand il a été découpé. Ce numéro était déjà pris par [ADR-015](0015-console-languages.md) (langues de la console), cité par le code et par `AGENT.md`. La décision des connexions prend donc le premier numéro libre, ADR-017, et le plan est corrigé en ce sens.

## Contexte

- ADR-014 (amendé par M7.02) décide d'implémenter Azure DevOps, Bitbucket et GitLab.com. Sur chacune de ces forges, l'accès d'une organisation repose sur des identifiants propres à cette organisation : application Entra ID et secret de webhook sur Azure DevOps, jeton chiffré sur Bitbucket et GitLab. GitHub a une App publique partagée, dont seule l'installation est propre à l'organisation (`Installations`, ADR-013).
- **Une réception de webhook doit trouver l'organisation avant d'y entrer.** La requête arrive sans session ni owner dans l'adresse. Une table d'organisation, filtrée par `ICurrentOrganization`, n'est pas lisible avant l'entrée, et `IgnoreQueryFilters` est interdit dans `src` sans exception (règle critique 8). GitHub résout ce cas par `Installations`, table **globale** avec une clé étrangère `OrganizationId`, lue par la réception avant la mise en file `ForOrganization`.
- **Un coordinateur doit lister les connexions de toutes les organisations** (synchronisation quotidienne, santé des connexions, hooks) pour mettre en file un enfant par organisation, sans lire plusieurs organisations dans une portée (ADR-013).
- **Une adresse de webhook doit désigner sa connexion sans lire le corps.** Un identifiant lu dans un corps non encore authentifié laisse l'appelant choisir le secret contre lequel il est vérifié. Le corps d'un service hook Azure DevOps `Minimal` ne porte d'ailleurs pas tous les identifiants.
- Rien de cela n'est construit : `Installations` est la seule table de connexion, et `Repositories` ne porte que `InstallationId`.

## Décision

### 1. `ForgeConnections` est une table globale

- La table `ForgeConnections` est **globale**, comme `Installations` : `TableClassification.Global`, **sans** `IsOrganizationOwned`.
- Elle porte une clé étrangère `OrganizationId` vers `Organizations` en `Restrict` et un index unique `UX(OrganizationId, Id)`. `Id` est un texte de 64 caractères au plus, choisi par l'instance, jamais par la forge.
- Ses secrets (`CredentialProtected`, `WebhookSecretProtected`, `WebhookSecretPreviousProtected`) sont chiffrés par `ISecretProtector`, comme ceux de `CodecovImports`. Aucun secret n'est stocké ni journalisé en clair.
- Ses colonnes sont définitives pour M7, M8 et M9 (M7.06) ; `Kind` et `Status` sont stockés en texte, une valeur de plus n'exige aucune migration.
- Le dépôt d'accès lit la table **sans filtre et sans `IgnoreQueryFilters`**, puisqu'elle n'en a pas. `FindOwnerAsync(id)` rend l'organisation et l'état d'une connexion, avant toute entrée dans une organisation.

### 2. Routes de webhook par connexion

- Toute route de webhook d'une forge à connexion porte l'identifiant de la connexion : **`/api/v1/{forge}/webhooks/{connectionId}`**, par exemple `/api/v1/azure-devops/webhooks/{connectionId}` et `/api/v1/bitbucket/webhooks/{connectionId}`.
- L'identifiant de connexion n'est **jamais** lu dans le corps. La réception (M7.21) lit la connexion par `FindOwnerAsync`, vérifie le secret courant ou précédent en temps constant par le validateur de la forge, stocke la livraison, puis la met en file par `JobRequest.ForOrganization`.
- Une connexion inconnue et un secret faux reçoivent la même réponse (`Webhook.InvalidSignature`, 401) : la route n'est pas un oracle d'existence des connexions.
- Ces routes n'existent pas chez Codecov : la règle critique 1 n'est pas touchée.

### 3. GitHub reste sur `Installations`

- **Aucune ligne `ForgeConnections` n'est créée pour GitHub.** Ses installations, sa réception `/api/v1/github/webhooks` signée par HMAC et `SyncInstallations` ne changent pas.
- `Repositories.ConnectionId` (M7.07) est un texte de 64 caractères au plus **sans clé étrangère**, ni vers `ForgeConnections` ni vers `Installations`. Pour GitHub, il vaut l'identifiant d'installation en texte, rempli depuis `InstallationId` par la migration. Pour les autres forges, il vaut `ForgeConnections.Id`.
- `ForgeRepositoryRef` lit `ConnectionId` pour toutes les forges ; un dépôt GitHub existant garde la même référence après la migration.

### 4. Chaque lecteur inter-organisations est une porte nommée

Chaque réception et chaque coordinateur qui lit `ForgeConnections` pour plusieurs organisations, ou avant l'entrée dans une organisation, est une **porte nommée** dans `OrganizationIsolationTests` (`CrossOrganizationEnqueueAllowList`, avec sa justification). Les portes prévues par le plan sont :

| Porte | Rôle | Micro-feature |
|---|---|---|
| `ForgeWebhookReception` | réception neutre, `FindOwnerAsync` puis `ForOrganization` | M7.21 |
| `ConnectionCoordinator` | `SyncConnections`, un `SyncConnectionRepositories` par connexion active | M7.10 |
| `ConnectionHealthCoordinator` | `CheckConnections`, une vérification par connexion | M7.14 |
| `HookCoordinator` | abonnements et hooks de chaque connexion | M7.23 |

Le lecteur des identifiants et le fournisseur de jetons d'une connexion, qui gardent un cache de processus, ont chacun leur entrée dans `MemoryCacheAllowList` (M7.39). Une nouvelle porte exige une ligne de plus dans ces listes et dans ce tableau ; aucune règle d'isolation n'est assouplie.

## Conséquences

- **Positives :**
  - une réception de webhook trouve l'organisation sans `IgnoreQueryFilters` ni appel à la forge, comme celle de GitHub ;
  - M8 et M9 réutilisent la table, la route, la réception et les coordinateurs sans migration ni nouvelle porte de forme différente ;
  - GitHub ne change ni de table, ni de route, ni de comportement ;
  - un secret de webhook propre à chaque connexion limite l'effet d'une fuite à une organisation et permet sa rotation (M7.13).
- **Négatives :**
  - une table globale de plus porte des données d'organisation (des secrets) hors du filtre EF : sa protection repose sur son dépôt, sur les portes nommées et sur la revue, comme pour `Installations` ;
  - `Repositories.ConnectionId` n'a pas de clé étrangère : l'intégrité entre un dépôt et sa connexion est tenue par le code (`ConnectOrganization`, `SyncConnectionRepositories`) et par les tests, pas par la base ;
  - deux chemins coexistent pour la connexion d'une organisation (`Installations` pour GitHub, `ForgeConnections` ailleurs) et deux coordinateurs quotidiens (`SyncInstallations`, `SyncConnections`).

## Prouvé et probable

**Prouvé** sur le dépôt et les ADR :

- `Installations` est classée globale dans `TableClassification`, avec une clé étrangère `OrganizationId` (ADR-013, M1.17).
- `IgnoreQueryFilters` est interdit dans `src` sans exception, et `OrganizationIsolationTests` tient des listes nommées des mises en file `ForOrganization` et des caches de processus.
- `ISecretProtector` chiffre déjà des secrets stockés (`CodecovImports`).

**Probable**, à vérifier par les micro-features citées :

- La colonne texte de 64 caractères suffit aux identifiants de connexion de M8 et M9 : l'identifiant est choisi par l'instance, la contrainte ne vient donc pas de la forge.
- Bitbucket (webhook de workspace) et GitLab (webhook de projet) acceptent une adresse de livraison qui porte un segment propre à la connexion ; leurs spikes (M8, M9) le vérifient.

## Risques résiduels

- Un dépôt de `ForgeConnections` mal écrit pourrait rendre la connexion d'une autre organisation : le test sur deux organisations de M7.06 (`ListForOrganizationAsync_TwoOrganizations_ShouldReturnOnlyItsOwn`) le couvre, pas une règle de build.
- L'identifiant de connexion figure dans l'adresse enregistrée chez la forge, donc visible de qui administre l'abonnement : il n'est pas un secret, et la sécurité repose sur le secret de webhook.
- Comme pour `OrganizationContext.Enter` (ADR-013), aucune règle de build ne limite les appelants de `FindOwnerAsync` : seules les listes de `OrganizationIsolationTests` et la revue gardent cet état.

## Alternatives écartées

- **`ForgeConnections` table d'organisation :** la réception ne pourrait la lire qu'en contournant le filtre, ce que la règle critique 8 interdit sans exception.
- **Identifiant de connexion ou d'organisation lu dans le corps :** l'appelant choisirait le secret de vérification avant d'être authentifié, et le corps `Minimal` d'Azure DevOps ne porte pas tous les identifiants.
- **Une seule route de webhook par forge et un secret d'instance :** une fuite toucherait toutes les organisations, et la rotation serait globale.
- **Migrer GitHub vers `ForgeConnections` :** l'App GitHub est partagée et ses installations ont leur propre cycle (`installation.created`, `suspend`) ; la migration ne servirait aucune forge et risquerait une régression avant la bascule.
- **Clé étrangère de `Repositories.ConnectionId` vers `ForgeConnections` :** impossible pour GitHub, qui n'y a pas de ligne ; une clé polymorphe vers deux tables n'existe pas dans le modèle EF commun aux deux fournisseurs.
