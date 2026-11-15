---
source-sha256: 9a6e1ebbd225f68090327b2f36be71d3431ba7cc31e7cb946bcadcf3c9b94246
---
# Comportements GitHub à vérifier sur deux vrais comptes (M4.02a)

L’ADR-013 repose sur des comportements GitHub qui n’ont pas pu être exécutés depuis le dépôt (« Probable » dans l’ADR).
Cette liste est exécutée par le propriétaire sur deux vrais comptes avant la bascule (M4.08) : le compte **A** (une
organisation GitHub) et le compte **B** (une seconde organisation, ou un compte personnel lorsqu’une ligne le
précise), tous deux avec l’App d’une instance de test installée. Chaque ligne est consignée comme **confirmée**,
**corrigée** (avec le changement de plan qu’elle ouvre) ou **non vérifiable** (avec son risque), et datée. Une
ligne confirmée passe de « Probable » à « Prouvé » dans l’ADR-013.

| # | Comportement | Procédure | Attendu (d’après le code) | Preuve à conserver | Résultat | Date |
|---|---|---|---|---|---|---|
| 1 | SAML et `GET /user/installations` | Sur une organisation avec authentification unique SAML (GitHub Enterprise Cloud), connectez-vous au Web avec un membre qui n’a **pas** autorisé le SSO pour l’OAuth App, puis avec un membre qui l’a fait. | Sans autorisation SSO, `user/installations` omet l’organisation : l’utilisateur n’en voit aucun dépôt (`GitHubUserGateway.ListInstallationsAsync`). Avec elle, l’organisation est listée. | Les deux listes de `/` après connexion ; la réponse de l’API (`gh api user/installations` avec le jeton de l’utilisateur). | | |
| 2 | Collaborateurs extérieurs dans cette liste | Ajoutez un collaborateur extérieur à un dépôt de A ; connectez-vous avec cet utilisateur. | `user/installations` liste l’installation de A avec seulement les dépôts que le collaborateur peut lire (`user/installations/{id}/repositories`) ; le rôle dans l’organisation est nul, l’accès au dépôt en lecture ou en écriture. | La liste des dépôts affichée ; le rôle résolu sur `/orgs/github/A`. | | |
| 3 | Contenu de l’événement `transferred` | Transférez un dépôt de A vers B. | Un événement `repository` `transferred` atteint l’instance avec l’identifiant du dépôt inchangé et le nouveau propriétaire ; `ReconcileRepositoryTransfer` ferme la ligne dans A, et `SyncInstallations` (clé de transfert) la crée dans B (`HandleWebhookDeliveryCommand`). | La livraison du webhook (Recent deliveries de l’App) et les lignes sur `/admin/operations`. | | |
| 4 | `installation_repositories` removed, reçu par l’origine | Pendant le transfert de la ligne 3. | L’installation de A reçoit `installation_repositories` avec le dépôt dans `repositories_removed` (quand A avait sélectionné des dépôts). | La livraison. | | |
| 5 | `organization` renamed | Renommez l’organisation A. | Un événement `organization` `renamed` arrive ; `RewriteRepositoryOwner` réécrit le propriétaire des dépôts de A ; l’ancien login ne se résout plus. | La livraison ; la nouvelle URL fonctionne, l’ancienne répond introuvable. | | |
| 6 | `installation_target` renamed, sur les deux types de compte | Renommez A (organisation) et B (compte personnel, si disponible). | Un événement `installation_target` `renamed` arrive pour chacun ; la ligne de l’organisation prend le nouveau login (`GitHubInstallationTargetEvent`). | Les livraisons ; `/admin/organizations`. | | |
| 7 | Secrets Actions après un transfert | Avant la ligne 3, ajoutez un secret de dépôt `CODECOV_TOKEN` au dépôt transféré. | Notez si GitHub conserve le secret après le transfert. Quoi qu’il fasse, un jeton de dépôt importé de A ne fonctionne plus dans B (refusé), et la rotation des jetons d’organisation de A est conseillée (`RotationAdvisedAt`). | Le refus de l’upload dans B ; le bandeau dans A. | | |
| 8 | Un dépôt public d’un autre compte lu par un jeton d’installation | Avec le jeton d’installation de B, lisez un dépôt public de A sur lequel B n’est pas installé. | GitHub répond (données publiques) ; l’instance ne s’en sert jamais, puisque toute lecture passe par l’installation de l’organisation propre au dépôt. | La réponse de l’API. | | |
| 9 | La forme `/repositories/{id}` sur chaque endpoint | Lancez une notification d’une pull request dans A, puis renommez le dépôt et lancez-en une autre. | Chaque appel ci-dessous fonctionne par identifiant de dépôt avant et après le renommage (inventaire ci-dessous). | Les appels dans les journaux (`github_requests_total` par endpoint) et les statuts après le renommage. | | |

Lorsque la ligne 1 ne peut pas être exécutée (aucune organisation GitHub Enterprise Cloud sous la main), consignez-la **non vérifiable**
avec son risque : un membre sans autorisation SSO voit moins de dépôts qu’il ne peut en lire sur GitHub ;
rien ne fuit.

## Inventaire des chemins de la passerelle

Les appels de `GitHubAppGateway` (jeton d’installation sauf mention contraire). Un dépôt suivi est
toujours adressé par son identifiant, pour qu’un renommage ne casse rien ; seule la recherche d’un dépôt par son nom
utilise le propriétaire et le nom.

| Appel | Chemin |
|---|---|
| Conversion du manifest (JWT de l’App) | `POST app-manifests/{code}/conversions` |
| Installations de l’App (JWT de l’App) | `GET app/installations`, `GET app/installations/{id}`, `POST app/installations/{id}/access_tokens` (`GitHubAppTokenProvider`) |
| Dépôts d’une installation | `GET installation/repositories` |
| Recherche d’un dépôt par son nom | `GET repos/{owner}/{name}/installation` (JWT de l’App), puis `GET repos/{owner}/{name}` |
| Appartenance d’un utilisateur | `GET orgs/{org}/memberships/{user}` |
| Branche | `GET repositories/{id}/branches/{branch}` |
| Commits | `GET repositories/{id}/commits?sha=…`, `GET repositories/{id}/commits/{sha}` |
| Comparaison | `GET repositories/{id}/compare/{base}...{head}` |
| Pull request et ses fichiers | `GET repositories/{id}/pulls/{n}`, `GET repositories/{id}/pulls/{n}/files` |
| Arbres et contenus | `GET repositories/{id}/git/trees/{sha}`, `GET repositories/{id}/contents/{path}?ref=…` |
| Statuts | `POST repositories/{id}/statuses/{sha}` |
| Check runs (M5.05) | `POST repositories/{id}/check-runs`, `PATCH repositories/{id}/check-runs/{id}` |
| Commentaires | `GET\|POST repositories/{id}/issues/{n}/comments`, `PATCH\|DELETE repositories/{id}/issues/comments/{id}` |

`GitHubUserGateway` (jeton utilisateur) : `GET user`, `GET user/installations`,
`GET user/installations/{id}/repositories`.
