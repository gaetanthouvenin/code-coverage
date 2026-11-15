# ADR-016 : Couverture détaillée des lignes, branches et méthodes

- **Statut :** proposé (M5.08, à accepter par le propriétaire avant M5.10)
- **Date :** 2026-11-15
- **Références :** `docs/PLAN.md` M5.08 à M5.15 ; `docs/ARCHITECTURE.md` §4.2 (`CommitReports`), §4.3 (`CCR1`), §7 et §10bis ; [ADR-008](0008-flag-views-from-sessions.md), [ADR-009](0009-codecov-parity-defaults.md) ; `docs/design/ui-mockup.html`, écran « Rapport détaillé »

## Contexte

- Le 2026-11-15, Capto envoie ses premiers rapports Cobertura (77,91 % sur 615 fichiers). Le propriétaire demande un rapport qui montre, par fichier, chaque sorte de couverture (lignes, branches, méthodes), comme ReportGenerator. La console n'affiche par fichier que les lignes, couvertes, partielles et manquées, et la couverture Codecov.
- **Ce que les parseurs lisent aujourd'hui** (`Infrastructure/Reports/Parsers/`, prouvé sur le code) :

  | Format | Branches | Méthodes |
  |---|---|---|
  | Cobertura | `condition-coverage="50% (1/2)"` d'une ligne `branch="true"` : couvertes et total gardés par ligne, plus les conditions manquées | aucune : les lignes des `<method>` sont lues comme celles de la classe, l'élément `<method>` est ignoré |
  | OpenCover | `BranchPoint` de chaque ligne : couvertes et total | aucune : `<Method>` ne sert qu'à parcourir ses `SequencePoint` |
  | LCOV | `BRDA` : couvertes et total, **sauf pour un fichier `.js`**, dont Codecov ignore les `BRDA` (oracle M1.15) | aucune : `FN` ne sert qu'à écarter les lignes des fonctions C++ décorées ; `FNDA`, `FNF` et `FNH` sont ignorés |
  | JaCoCo | `mb` et `cb` de chaque ligne | la ligne d'une `<method>` qui porte un compteur `COMPLEXITY` est marquée méthode (`IsMethod`), sans dire si la méthode est couverte ; le compteur `METHOD` est ignoré |
  | Clover | une ligne `cond` vaut deux branches (`truecount`, `falsecount`) | une ligne `type="method"` est marquée méthode, son `count` est celui de la ligne |

- **Ce que le modèle garde.** `LineCoverage` garde par ligne les hits, les branches couvertes et leur total, et le drapeau `IsMethod` : les branches couvertes d'un fichier sont donc déjà dans le blob `CCR1` (`CommitReports.Data`, sessions, uploads). `CoverageTotals.Branches` et `CoverageTotals.Methods` sont les compteurs de Codecov, des **nombres de lignes** (lignes à branches, lignes de méthode), sans rien de couvert. `CommitReports.FileIndex` (`CFI1`) ne garde par fichier que hits, misses, partials et ces deux compteurs. Aucune méthode n'est stockée avec son état.
- **Codecov ne connaît pas ces chiffres.** Sa formule `hits / (hits + misses + partials)` reste le chiffre de tête (règle critique 1) ; ses `branches` et `methods` de totaux restent des nombres de lignes, publiés tels quels.

## Décision

### 1. Trois chiffres nommés de plus, à côté du chiffre Codecov

| Chiffre | Couvert | Couvrable | Source |
|---|---|---|---|
| **Couverture (Codecov)**, inchangée | hits | hits + misses + partials | modèle de ligne actuel |
| **Lignes** (définition de ReportGenerator) | lignes exécutées au moins une fois (`Hits > 0`), partielles comprises | lignes du rapport | modèle de ligne actuel |
| **Branches** | somme des branches couvertes des lignes | somme des branches des lignes | modèle de ligne actuel |
| **Méthodes** | méthodes exécutées au moins une fois | méthodes du rapport | nouveau canal des méthodes (§2) |

Chaque pourcentage passe par `CoverageMath.Ratio` (`null` sans rien de couvrable, affiché `—`) puis par `coverage.precision` et `coverage.round` du `codecov.yml`, comme le chiffre Codecov. Les « lignes » diffèrent de Codecov par les partielles, comptées couvertes, et par une ligne à branches dont aucune branche n'est prise mais qui est exécutée (manquée pour Codecov, couverte ici) : la console le dit dans l'aide de la colonne.

### 2. Les méthodes passent par un canal à part, jamais par `IsMethod`

- `Domain/Coverage/MethodCoverage` (`Line`, `Hits`) : la première ligne d'une méthode et le nombre de fois qu'elle a été exécutée. `ParsedFile`, `FileCoverage` et `CoverageReport` portent leurs méthodes, `null` quand la source ne les connaît pas (blob de version 1).
- `IsMethod`, `LineKind.Method` et `CoverageTotals.Methods` gardent exactement leur sens et leurs valeurs Codecov : statuts, commentaires, badges, `Commits.Methods` et l'API ne changent pas. Un test le prouve sur toutes les fixtures (totaux Codecov identiques avant et après).
- **Lecture par format :** Cobertura, une `<method>` dont la première ligne est la plus petite de ses `<line>`, exécutée si l'une d'elles l'est (le format n'a pas de compteur de méthode) ; OpenCover, `<Method>` à la ligne de son premier `SequencePoint`, exécutée selon le `vc` de son `MethodPoint` ; LCOV, `FN` et `FNDA` (hors fonctions C++ décorées, déjà écartées) ; JaCoCo, `<method line>` avec son compteur `METHOD` (`covered > 0`), qu'elle ait ou non un compteur `COMPLEXITY` ; Clover, chaque ligne `type="method"` et son `count`. Les totaux d'un fichier dans le rapport (`FNF`, `FNH`, `<metrics methods coveredmethods>`) ne sont pas lus : ils sont recalculés.
- **Fusion :** deux méthodes de la même ligne n'en font qu'une, avec le plus grand nombre d'exécutions, dans un rapport comme entre sessions. Le décalage des lignes d'une session reportée (`LineShifter`) et les filtres de chemins (`ignore`, `paths`, `fixes`) s'appliquent aux méthodes comme aux lignes ; une méthode dont la ligne disparaît au décalage est retirée.
- **Inconnu contre zéro :** un fichier fusionné dont une partie ne connaît pas ses méthodes (session reportée depuis un blob de version 1) a des méthodes inconnues, affichées `—`, jamais 0.

### 3. Stockage : deux formats de blob versionnés, aucune colonne

- **`CCR1` version 2** (même magic, octet de version 2) : après les lignes de chaque fichier, un octet de drapeaux (bit 0 : méthodes connues), puis `varint methodCount` et, par méthode, `varint deltaLine` et `varint hits` (borné à `uint32` comme celui d'une ligne). Il sert aux trois blobs : `UploadCoverages.Data`, `CommitSessions.Data` et `CommitReports.Data`.
- **`CFI2`** (index de fichiers, nouveau magic) : les cinq compteurs de `CFI1`, puis `executedLines`, `coveredBranches`, `totalBranches`, un octet de drapeaux (bit 0 : méthodes connues) et, si elles le sont, `coveredMethods` et `totalMethods`.
- Les versions 1 restent lisibles indéfiniment : un `CFI1` donne des fichiers sans détail, un `CCR1` version 1 des fichiers sans méthodes. Seule la version 2 est écrite.
- **Aucune migration** : les colonnes `Data` et `FileIndex` sont des blobs dont le format est l'affaire du codec (§4.3). `Commits` et `CommitFlagTotals` ne changent pas : le total du rapport détaillé est la somme de ses fichiers, calculée à la lecture.

### 4. Aucun rattrapage automatique

Un commit traité avant la release garde un index `CFI1` : le rapport détaillé affiche `—` dans les colonnes de détail avec un bandeau, et l'action « Retraiter » existante (brut conservé `Retention:RawDays`, 30 jours) le reconstruit. Le prochain upload de la branche par défaut donne le détail sans rien faire.

### 5. Exposition

- `FileTotals` porte un `CoverageDetail?` (`null` pour un index `CFI1`). La requête `GetCommitFiles` ne change pas de forme ; la vue par flag (ADR-008) calcule le détail depuis les sessions.
- L'Api ajoute un objet `detail` facultatif à chaque fichier de `GET /api/v1/repos/{forge}/{owner}/{repo}/commits/{sha}/files` : ajout compatible, cette route n'est pas une route Codecov.
- Le Web ajoute l'écran « Rapport détaillé » (`/{forge}/{owner}/{repo}/commit/{sha}/report`), ouvert depuis le panneau Fichiers du commit, dont l'arbre Codecov ne change pas.

## Conséquences

- **Positives :**
  - le chiffre Codecov, les statuts, le commentaire, les badges et l'historique ne bougent pas, et un dépôt qui vient de Codecov ne voit aucune différence hors de l'écran ajouté ;
  - les branches viennent de données déjà stockées : seules les méthodes demandent un changement des parseurs ;
  - aucune migration, aucune table, aucun rattrapage à exécuter.
- **Négatives :**
  - le blob grossit de deux varints par méthode, quelques centaines d'octets pour un fichier de quelques dizaines de méthodes, avant Brotli ;
  - **un retour arrière** vers une image qui ne lit que la version 1 rend illisibles les rapports écrits depuis : la release qui écrit la version 2 le dit dans ses notes, et `ReprocessCommit` reconstruit tout depuis le brut après une remontée ;
  - le détail d'un commit traité avant la release n'existe qu'après un retraitement.
- **Écarts nommés avec ReportGenerator**, non configurables, parce que la parité Codecov du modèle de ligne passe d'abord (ADR-009) :
  - LCOV d'un fichier `.js` : aucune branche, puisque Codecov en ignore les `BRDA` ;
  - `parsers.*.partials_as_hits` : une ligne convertie en ligne simple perd ses branches, comme chez Codecov ;
  - Clover : deux branches par ligne `cond`, vrai et faux ;
  - deux méthodes qui commencent sur la même ligne (lambda, méthode synthétique) comptent pour une ;
  - Cobertura : une méthode est couverte dès qu'une de ses lignes l'est, faute de compteur de méthode ;
  - les « méthodes entièrement couvertes » de ReportGenerator ne sont pas calculées.
