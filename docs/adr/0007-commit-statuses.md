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
- Le préfixe de contexte est réglable à chaud, par organisation (`Notifications:StatusContextPrefix`, ADR-013) :
  - **`coverage`** par défaut, pendant le pilote (statuts `coverage/project`, `coverage/patch`) ;
  - **`codecov`** à la bascule de chaque organisation, à sa propre date, ce qui reprend les noms déjà exigés par la protection de branche.

## Conséquences

- **Positives :**
  - le pilote ne perturbe pas les statuts Codecov existants ;
  - la bascule se fait en changeant un paramètre, sans toucher aux règles de protection ;
  - les builds Azure Pipelines sont couverts sans dépendre d'un check suite GitHub.
- **Négatives :**
  - pas d'annotations de lignes dans l'onglet « Files changed » avant M5 ;
  - deux implémentations de publication à terme (statuts et checks).

## Amendement du 2026-11-13 (M5.05)

Décision du propriétaire, prise avec le lot M4/M5 :

- Les **Check Runs remplacent les statuts** (comme Codecov), avec les mêmes noms (`{préfixe}/project`, `{préfixe}/patch`, `{préfixe}/changes` et les entrées nommées), quand trois conditions sont réunies : le dépôt est sur GitHub (ADR-014), l'organisation a allumé `Notifications:ChecksEnabled` (cinquième paramètre d'organisation, éteint par défaut, jamais secret) et le `codecov.yml` ne dit pas `github_checks: false`.
- Le Check Run du statut `patch` par défaut porte les annotations des lignes ajoutées non couvertes (`github_checks.annotations`, vrai par défaut), 50 par appel : création puis `PATCH` du même Check Run, dont l'identifiant est conservé (`PostedStatuses.CheckRunId`). Une notification qui change l'état ou la description crée un nouveau Check Run, qui remplace l'ancien à l'affichage.
- Un refus de GitHub (403 quand l'installation n'a pas la permission Checks, ou 404, que GitHub répond à une installation qui ne voit pas les checks d'un dépôt privé, journalisé sous un événement à part) **revient au statut** pour cette notification, sans jamais faire échouer le job. Un 422 est une requête invalide : un statut ne la corrigerait pas, c'est un échec.
- Quand un nom passe d'un statut à un Check Run (ou l'inverse), la publication précédente reçoit aussi le nouvel état, pour qu'un contrôle requis ne garde jamais un état ancien.
- Le manifest de `/setup` demande `checks: write` ; une App existante reçoit la permission par une action du propriétaire, puis chaque organisation l'accepte. L'interrupteur reste éteint jusque-là.
- Écart nommé et configurable avec Codecov : tant que l'interrupteur est éteint, les statuts sont publiés là où Codecov publierait des Check Runs. La bascule (M4.08) l'allume par organisation.
- La phrase « la permission `Checks` de l'App reste à « none » jusque-là » de la décision initiale ne vaut plus à partir de M5.05.
