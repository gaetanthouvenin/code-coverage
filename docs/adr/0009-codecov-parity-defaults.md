# ADR-009 : Parité Codecov par défaut sur les particularités

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §4bis.3, §6.3, §6.4, §7.6 et §13.3 ; `docs/PLAN.md` M1 (oracle de totaux)

## Contexte

- La règle directrice du projet : un dépôt qui fonctionne avec Codecov garde les mêmes chiffres, les mêmes statuts et les mêmes secrets après la bascule.
- Pendant le pilote, les totaux sont comparés à Codecov (±0,01 %) et à un oracle ReportGenerator. Plusieurs comportements de Codecov sont discutables, mais tout écart non expliqué fait perdre confiance et retarde la bascule.
- Particularités identifiées :
  - `after_n_builds` compte aussi les sessions reportées (carryforward) ;
  - les lignes de type méthode des rapports Clover sont comptées comme des lignes ;
  - en LCOV, les données de branche priment sur les hits ;
  - aucune correspondance de chemin sur le seul nom de fichier.

## Décision

- Par défaut, le service **reproduit le comportement de Codecov** sur chacune de ces particularités.
- **Chacune est réglable** et documentée, par exemple `Notifications:AfterNBuildsCountsCarryforward` (vrai par défaut) et `Coverage:AllowFilenameOnlyMatch` (faux par défaut).
- Tout écart avec l'oracle est soit nul, soit expliqué et consigné dans `expected-totals.json` des tests de parseurs.

## Conséquences

- **Positives :**
  - les chiffres du pilote sont comparables à ceux de Codecov, ce qui sécurise la bascule ;
  - une équipe peut choisir explicitement un comportement plus strict, réglage par réglage.
- **Négatives :**
  - certains comportements contestables sont conservés par défaut ;
  - chaque réglage ajoute un cas de test et, le cas échéant, une ligne au catalogue des paramètres.
