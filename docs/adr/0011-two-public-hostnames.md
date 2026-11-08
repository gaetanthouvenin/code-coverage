# ADR-011 : Deux noms d'hôte publics, aucun routage par chemin

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §2, §8.1, §12.2 et §12.3

## Contexte

- Deux hôtes sont déployés (ADR-002) : l'Api, qui doit être joignable par GitHub (webhooks), par les agents CI (uploads) et par camo (badges des README), et le Web, qui peut rester sur le réseau interne.
- Servir les deux sous un même nom d'hôte imposerait un routage par chemin (préfixes, réécriture, `PathBase`), source de conflits entre les routes Blazor, les routes de l'API et les URL compatibles Codecov (`/gh/{owner}/{repo}/…`).
- La GitHub App déclare une URL de webhook (Api) et une URL de callback OAuth (Web).

## Décision

- L'Api et le Web sont publiés sur **deux noms d'hôte distincts** : par exemple `coverage-api.example.com` pour l'Api et `coverage.example.com` pour le Web (les noms réels sont choisis au déploiement).
- **Aucun routage par chemin** : chaque hôte est servi à la racine. Le reverse proxy (Caddy sur VM, ingress sur Azure Container Apps) termine TLS et route par nom d'hôte.
- Les URL publiques sont des paramètres (`Urls:ApiPublicUrl`, `Urls:WebPublicUrl`) utilisés pour les liens du commentaire de PR, l'audience OIDC et la configuration de l'App.

## Conséquences

- **Positives :**
  - aucune collision de routes ni de `PathBase` ; configuration du proxy triviale ;
  - le Web peut être restreint au réseau interne ou au VPN sans affecter l'Api ;
  - cookies d'authentification du Web isolés de l'Api.
- **Négatives :**
  - deux enregistrements DNS et deux certificats à gérer ;
  - les liens croisés (commentaire de PR vers le Web, Web vers les badges) passent par les URL publiques configurées.
