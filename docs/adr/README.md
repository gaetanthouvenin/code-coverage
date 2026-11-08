# Décisions d'architecture (ADR)

Chaque ADR suit le format **Contexte / Décision / Conséquences** et porte un statut (« proposé », « accepté », « remplacé par ADR-nnn »). La liste fait écho à `docs/ARCHITECTURE.md` §13.1 ; les décisions encore ouvertes sont suivies dans `docs/PLAN.md` §11.

| # | Titre | Statut |
|---|---|---|
| [ADR-001](0001-mediator.md) | Médiateur martinothamar `Mediator` 3.x | accepté (spike M0.07) |
| [ADR-002](0002-two-hosts.md) | Deux hôtes, worker uniquement dans l'Api | accepté |
| [ADR-003](0003-single-tenant.md) | Mono-tenant | remplacé par ADR-013 |
| [ADR-004](0004-no-octokit.md) | Ni Octokit ni Octokit.Webhooks | accepté |
| [ADR-005](0005-database-job-queue.md) | File de jobs en base, portable, sans Hangfire | accepté |
| [ADR-006](0006-settings-revision-polling.md) | Paramètres propagés par interrogation de `SettingsRevision` | accepté |
| [ADR-007](0007-commit-statuses.md) | Statuts de commit en v1, Checks en M5 | accepté |
| [ADR-008](0008-flag-views-from-sessions.md) | Vues par flag calculées à partir des sessions | accepté |
| [ADR-009](0009-codecov-parity-defaults.md) | Parité Codecov par défaut sur les particularités | accepté |
| [ADR-010](0010-bigint-keys-chunked-raw.md) | Clés `bigint` identity et brut en base par morceaux | accepté |
| [ADR-011](0011-two-public-hostnames.md) | Deux noms d'hôte publics, aucun routage par chemin | accepté |
| [ADR-012](0012-uploader-distribution.md) | Uploader distribué en binaires servis par l'Api | accepté |
| [ADR-013](0013-organization-isolation.md) | Isolation entre organisations sur une instance partagée | proposé, jusqu'à M1.16d2 |
