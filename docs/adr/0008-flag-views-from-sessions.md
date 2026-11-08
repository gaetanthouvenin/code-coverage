# ADR-008 : Vues par flag calculées à partir des sessions

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §6.5, §7 et §11.3 ; `docs/PLAN.md` M1 (vue par flag)

## Contexte

- Un commit reçoit plusieurs uploads, chacun porteur de flags (monorepo). Une session est créée par upload, plus des sessions reportées (carryforward).
- Les statuts, le commentaire, les badges et l'historique ont besoin de la couverture **par flag**, éventuellement restreinte à des `paths`.
- Matérialiser un rapport complet par flag et par commit multiplierait le volume des blobs, alors que la plupart de ces vues ne sont jamais lues.

## Décision

- Le rapport fusionné du commit (`CommitReport`) et les sessions (blobs par upload) sont les seules données détaillées stockées.
- Une vue par flag (`ReportView`) est **calculée à la demande** : fusion des seules sessions qui portent un des flags, puis application de l'union des `paths` des flags et des `paths` du statut. Le résultat est mis en cache en mémoire par `(commitId, revision, flagsKey, pathsKey)` pendant 10 minutes.
- **Seuls les totaux sont matérialisés** par flag (`CommitFlagTotals`), écrits par `FinalizeCommit`, pour les badges, l'historique et les listes.

## Conséquences

- **Positives :**
  - volume de stockage maîtrisé ; aucune incohérence possible entre une vue et les sessions dont elle dérive ;
  - badges et historique servis par une simple lecture de totaux.
- **Négatives :**
  - le calcul d'une vue détaillée coûte une fusion de sessions, amortie par le cache ;
  - le cache est propre à chaque hôte et invalidé par la révision du rapport ; il n'est pas partagé entre réplicas.
