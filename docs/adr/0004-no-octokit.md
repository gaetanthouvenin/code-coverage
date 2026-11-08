# ADR-004 : Ni Octokit ni Octokit.Webhooks

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §3.3, §8.2, §8.3 et §8.6

## Contexte

- Le service appelle une quinzaine d'endpoints GitHub : statuts, commentaires d'issue, compare, fichiers de PR, arbres, contenus, commits, installations et dépôts accessibles à un utilisateur.
- Il a besoin d'un contrôle fin sur la pagination, sur le champ `patch` des diffs (absent au-delà des limites GitHub), sur les en-têtes de rate limit et sur la mise en cache conditionnelle.
- Il reçoit des webhooks signés (HMAC-SHA256) et doit accepter la rotation du secret (secret courant et secret précédent).
- Octokit expose un modèle large, avec sa propre pagination et sa propre gestion des erreurs ; Octokit.Webhooks impose son propre pipeline de réception.

## Décision

- **Aucune dépendance à Octokit ni à Octokit.Webhooks.**
- Les appels passent par un **`HttpClient` typé** (`Microsoft.Extensions.Http.Resilience`), avec des records `System.Text.Json` générés par source pour les seules charges utiles utilisées.
- Les jetons d'installation sont obtenus par `GitHubAppTokenProvider` (JWT de l'App signé avec la clé privée stockée chiffrée).
- La vérification des webhooks est **maison** : HMAC-SHA256 sur le corps brut, comparaison par `CryptographicOperations.FixedTimeEquals`, contre le secret courant puis le précédent. Elle est derrière le port `IWebhookSignatureValidator`.
- La réception passe par un **contrôleur MVC** (`GitHubWebhooksController`, `[AllowAnonymous]`) qui met la livraison en file et répond 202, sans appel GitHub dans la requête.

## Conséquences

- **Positives :**
  - contrôle exact de la pagination, du champ `patch`, des en-têtes `X-RateLimit-*` et des réponses 304 ;
  - aucune réflexion de sérialisation, charges utiles minimales, dépendances réduites ;
  - les tests rejouent des réponses enregistrées (WireMock.Net, fixtures `tests/fixtures/github`).
- **Négatives :**
  - les records et les appels sont à écrire et à maintenir pour chaque endpoint utilisé ;
  - les évolutions de l'API GitHub ne sont pas absorbées par une bibliothèque tierce : elles sont couvertes par les tests de contrat sur fixtures.
