# ADR-002 : Deux hôtes, worker uniquement dans l'Api

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §2, §3.2, §4bis.2, §6.1 et §12 ; `docs/CONVENTIONS.md` §16.3 point 1

## Contexte

- L'utilisateur exige que l'API et les vues vivent dans **deux projets distincts**, au lieu d'un seul projet `*.Web` réunissant Blazor Server et contrôleurs.
- Pour le Web, deux options existaient (CONVENTIONS §16.3 point 1) :
  - (a) référencer l'Application et envoyer les requêtes en process via le médiateur ;
  - (b) appeler l'Api en HTTP, ce qui impose un client typé, la conversion des `ProblemDetails` en exceptions et une authentification de service à service.
- Le traitement des uploads et des webhooks est asynchrone (principe non négociable n°4) et doit tourner dans un seul type d'hôte, joignable par GitHub et les agents CI.

## Décision

- La solution publie **deux hôtes ASP.NET Core** :
  - **Api** (`CodeCoverage.Api`) : API REST `/api/v1`, uploads, webhooks, badges, binaires de l'uploader, Swagger et Scalar. Elle héberge le **`JobWorker`** et applique les migrations au démarrage ;
  - **Web** (`CodeCoverage.Web`) : UI Blazor Server (InteractiveServer). Elle n'a ni worker ni migrations.
- Le Web **exécute l'Application en process** via le médiateur (option a) : mêmes commandes, mêmes requêtes, mêmes validations et mêmes autorisations que l'Api.
- Le worker ne démarre que si `Worker__Enabled` vaut `true` (valeur par défaut sur l'Api). Un réplica purement public peut le désactiver.
- Les deux hôtes partagent la même base : un job mis en file par le Web (re-notification, relance) est pris en charge par la scrutation du worker de l'Api.

## Conséquences

- **Positives :**
  - aucune couche HTTP interne entre le Web et l'Api : pas de client typé, pas de jeton de service, pas de double sérialisation ;
  - une seule implémentation de chaque règle métier, appliquée de la même façon par les deux hôtes ;
  - le Web peut rester sur le réseau interne pendant que l'Api est exposée (ADR-011).
- **Négatives :**
  - les deux hôtes ont besoin de la même chaîne de connexion et du même trousseau Data Protection ;
  - les paramètres modifiés dans un hôte doivent être propagés à l'autre (ADR-006) ;
  - la composition (`Program.cs`, `Configuration/*`) est en partie dupliquée entre Api et Web.
- **Garde-fous :** les tests d'architecture interdisent aux types des namespaces `Api.*` et `Web.*` de référencer le Domain, sauf `Mapping`, `Program` et `Configuration` (patron « envoi-et-conversion », PLAN §1.4).
