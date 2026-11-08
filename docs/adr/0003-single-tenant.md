# ADR-003 : Mono-tenant

- **Statut :** remplacé par [ADR-013](0013-organization-isolation.md) le 2026-11-08 (décision conservée pour l'historique)
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §1.3, §4.2, §8.1 et §13.1 ; `docs/PLAN.md` §11-2

## Contexte

- Une bibliothèque multi-tenant (un `TenantId` par table et un filtre global de tenant) permet d'isoler les données de plusieurs clients dans un même déploiement.
- Ce service remplace `codecov.io` pour **une seule organisation GitHub** (`<owner>`, fixée au déploiement par le paramètre `GitHub:Organization`), avec une GitHub App privée installée sur cette organisation. Le multi-tenant est explicitement hors périmètre (ARCHITECTURE §1.3).
- L'isolation utile n'est pas entre clients, mais **entre dépôts** : un utilisateur ne doit voir que les dépôts auxquels GitHub lui donne accès.
- Le schéma de données doit être figé avant le premier modèle EF et ses migrations (M0.14) : ajouter une dimension tenant après coup touche toutes les tables.

## Décision

- Le service est **mono-tenant** : une organisation GitHub, un déploiement. Il n'utilise aucune bibliothèque multi-tenant et aucune table ne porte de `TenantId`.
- L'isolation se fait **par dépôt**, calquée sur les droits GitHub de l'utilisateur connecté (contrôle d'accès par dépôt, mis en cache et rafraîchi).
- Plusieurs organisations GitHub restent possibles dans un même déploiement grâce à la table `Installations` et à la colonne `Owner` des dépôts, sans notion de tenant.

## Conséquences

- **Positives :**
  - modèle de données, requêtes et index plus simples ; aucun filtre global de tenant à maintenir ;
  - aucune dépendance à un package privé (le service se construit avec les seuls packages de nuget.org).
- **Négatives :**
  - servir une autre entité dans un même déploiement demanderait d'ajouter un `TenantId`. Cette évolution reste une **migration mécanique** (colonne, index composites, filtre global) ;
  - les droits d'administration sont globaux au déploiement (`Access:AdminLogins`).
- **Suivi :** la décision figure dans les décisions ouvertes du plan (`docs/PLAN.md` §11-2) et doit être confirmée avant M0.14. Si elle est remise en cause, ce document est mis à jour avant la création du modèle EF.
