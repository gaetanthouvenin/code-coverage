# ADR-007 : Statuts de commit en v1, Checks en M5

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §1.2, §7.7, §8.1, §8.5 et §14 ; `docs/PLAN.md` M4 et M5

## Contexte

- Les dépôts actuels exigent les statuts Codecov (`codecov/project`, `codecov/patch`) dans leurs règles de protection de branche.
- GitHub offre deux mécanismes : les **statuts de commit** (simples, valables pour tout SHA, y compris les builds Azure Pipelines) et les **Check Runs** (annotations de lignes, mais liés à une GitHub App et plus riches à produire).
- Pendant le pilote, Codecov et ce service publient en parallèle sur les mêmes commits : leurs noms de statut ne doivent pas entrer en collision.
- La bascule ne doit pas obliger à modifier la protection de branche de chaque dépôt.

## Décision

- Le MVP publie des **statuts de commit** `project`, `patch` et `changes` (ce dernier désactivé par défaut, comme chez Codecov).
- Les **Check Runs**, avec annotations des lignes du patch non couvertes, arrivent en **M5**, avec les mêmes noms de contexte. La permission `Checks` de l'App reste à « none » jusque-là.
- Le préfixe de contexte est réglable à chaud (`Notifications:StatusContextPrefix`) :
  - **`coverage`** pendant le pilote (statuts `coverage/project`, `coverage/patch`) ;
  - **`codecov`** à la bascule (M4), ce qui reprend les noms déjà exigés par la protection de branche.

## Conséquences

- **Positives :**
  - le pilote ne perturbe pas les statuts Codecov existants ;
  - la bascule se fait en changeant un paramètre, sans toucher aux règles de protection ;
  - les builds Azure Pipelines sont couverts sans dépendre d'un check suite GitHub.
- **Négatives :**
  - pas d'annotations de lignes dans l'onglet « Files changed » avant M5 ;
  - deux implémentations de publication à terme (statuts et checks).
