# ADR-005 : File de jobs en base, portable, sans Hangfire

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §4.1, §6.1 et §6.6 ; `docs/PLAN.md` M0.22 à M0.25

## Contexte

- Aucun travail lourd ne doit être fait dans une requête HTTP (principe non négociable n°4) : uploads et webhooks sont traités en arrière-plan.
- Le service supporte **PostgreSQL et SQL Server** avec un seul modèle EF Core.
- Le traitement exige :
  - la mise en file dans la **même transaction** que la modification qui la déclenche (outbox intégré) ;
  - la sérialisation par clé (un seul `FinalizeCommit` par commit à la fois), tout en traitant en parallèle les uploads d'un même commit ;
  - le dédoublonnage et l'anti-rebond (`NotifyCommit`) ;
  - la reprise après la perte d'un worker (bail) et des jobs récurrents non dupliqués entre réplicas.
- Le déploiement doit rester minimal : ni Redis ni broker de messages (ARCHITECTURE §2).

## Décision

- Une **file de jobs en base** (table `Jobs`), implémentée dans `Infrastructure/Jobs/` : `SqlJobQueue` (`IJobQueue`), `JobWorker`, `JobDispatcher`, `LeaseSweeper`, `RecurringJobScheduler` et `JobWakeUpSignal`.
- **Réclamation portable** : `FOR UPDATE SKIP LOCKED` sous PostgreSQL, `WITH (ROWLOCK, UPDLOCK, READPAST)` sous SQL Server. Ce dialecte est le seul SQL brut de la file ; il est fourni par chaque assembly de migrations.
- **Index uniques filtrés** :
  - `DedupKey` filtré sur `State = 'Queued'` : un job identique déjà en attente est fusionné ou décalé (anti-rebond) ;
  - `LockKey` filtré sur `State = 'Running'` : un seul job d'une clé donnée tourne, sans verrou consultatif ni `sp_getapplock`.
- Bail de 5 minutes renouvelé par heartbeat, clôture conditionnée au propriétaire, reprise par `LeaseSweeper` compatible avec le dédoublonnage.
- **Pas de Hangfire**, ni d'autre ordonnanceur tiers.

## Conséquences

- **Positives :**
  - atomicité entre la donnée métier et le job, sans outbox séparé ;
  - aucune infrastructure supplémentaire ; état des jobs visible et relançable dans `/admin/operations` ;
  - comportement identique sur les deux fournisseurs, vérifié par des tests concurrents sous Testcontainers.
- **Négatives :**
  - la file est du code à maintenir (réclamation, bail, backoff, récurrence) ;
  - la scrutation (2 s) ajoute une petite latence aux jobs mis en file par le Web ; le réveil en process la supprime pour l'Api ;
  - le débit est borné par la base, ce qui suffit largement à la volumétrie d'une organisation.
