# Design de la console web

Porte de validation **M3.18** de `docs/PLAN.md`. Chaque page de M3.19 à M3.43 se construit à partir d'un écran validé ci-dessous.

## Validation

| | |
|---|---|
| **Statut** | **Validé** par le propriétaire le 2026-11-08 |
| **Maquette** | [Coverage console mockups](https://claude.ai/artifact/Kt43DrVwwtS4F85CDD9PNA) (version 4, artefact privé) |
| **Copie versionnée** | [`docs/design/ui-mockup.html`](design/ui-mockup.html), lisible sans compte |

La maquette remplace la référence visuelle précédente, qui décrivait une organisation unique ; l'ancienne version reste dans l'historique git.

La version 3 aligne la maquette validée sur [ADR-014](adr/0014-multi-forge.md), après la validation. Seuls changent les routes, le libellé du sélecteur d'organisation et les tables d'adresses de ce document.

La version 4 ajoute la forge à `/admin/organizations` et retire les logins réservés de cet écran, validés par le propriétaire le 2026-11-08.

La documentation dans l'application (**M4.02n**, construite par M4.02o) ajoute l'écran `#docs`, l'entrée « Documentation » de la barre du haut et les boutons d'aide contextuelle ; elle est **validée** par le propriétaire le 2026-11-15 (section « Documentation dans l'application »).

Le choix de la langue (**M4.02q**, construit par M4.02t et M4.02v) ajoute le sélecteur de langue au menu utilisateur, au menu mobile, à la barre publique, à la connexion et à `/setup`, et l'état « Page pas encore traduite » de `#docs` ; il est **validé** par le propriétaire le 2026-11-15 (section « Langues de la console »).

Ouvrir la page donne l'index des écrans. La barre du bas change l'écran, son état, le rôle, la langue, le thème et la largeur ; l'ancre `#{écran}--{état}` désigne un état précis, par exemple `#admin-organizations--dlg-suspend`.

### Décisions prises à la validation

| # | Sujet | Décision |
|---|---|---|
| 1 | Navigation | La barre du haut en pilule de §10bis.1, avec les entrées Organisation (administrateurs de l'organisation) et Instance (administrateurs d'instance) et leurs sections en `CcPillTabs`. Le « menu latéral » de M3.19 se lit ainsi. |
| 2 | Organisation `Pending` ou `Suspended` | Visible seulement dans `/admin/organizations`. Un membre reçoit le même « introuvable ou inaccessible » que pour un compte inconnu, sans bandeau (ADR-0013, refus uniformes ; M3.43a). |
| 3 | Organisation suspendue | Aucune action de réadmission dans la console : M1.22a n'active que les lignes `Pending`. Seule une réinstallation réactive une suspension venue de la désinstallation. |
| 4 | Contenu sans base dans le plan | Retirés : cloche de notifications, graphiques d'uploads et de patchs, page côté GitHub, cartes KPI des listes, documentation d'API et extraits de pipeline. Les écrans d'instance n'affichent aucun dépôt. |
| 5 | Page racine `/orgs/{forge}/{org}` | Aucune : le plan n'en nomme pas, l'entrée Organisation ouvre `/orgs/{forge}/{org}/repositories`. |
| 6 | Détail par fichier | L'arbre du commit affiche lignes, couvertes, partielles et manquées, et la source annotée les totaux du fichier. Pas d'onglet Fichiers par branche. |
| 7 | Forge dans `/admin/organizations` | Toujours visible, par une colonne et un champ du dialogue d'admission, avec seulement GitHub au départ. |
| 8 | Logins réservés | Absents de la console : ni motif de suspension, ni dialogue d'erreur. ADR-014 les supprime en M3.43a. |

### Sélecteur d'organisation

L'organisation vient toujours de l'adresse ; aucun choix n'est mémorisé.

| Page | Comportement |
|---|---|
| `/` | « Toutes les organisations », ou filtre la liste sur une seule |
| `/{forge}/{owner}/{repo}` et ses sous-pages | Affiche le propriétaire du dépôt ; en choisir un autre mène à la liste de cette organisation |
| `/orgs/{forge}/{org}/*` | Même section de l'autre organisation si l'utilisateur l'administre, sinon sa liste |
| Options | Seulement les organisations `Active` de l'utilisateur ; masqué sans organisation |
| Même nom sur deux forges | Les deux options ajoutent la forge, par exemple `acme-labs (GitHub)` et `acme-labs (GitLab)` ; sinon le login suffit |
| Sous-groupe GitLab, projet Azure DevOps | L'adresse porte `group:sub:sub2` ou `org:project` ; le sélecteur n'affiche que l'organisation, avant le premier `:` |

## Adresses

Les adresses suivent [ADR-014](adr/0014-multi-forge.md). Les forges sont GitHub, Azure DevOps, Bitbucket et GitLab ; seul GitHub est implémenté au départ, et les exemples de la maquette portent donc `github`.

| Usage | Forme |
|---|---|
| Dépôt | `/{forge}/{owner}/{repo}/…` |
| Organisation | `/orgs/{forge}/{org}/…` |
| Sans organisation, inchangées | `/`, `/account/tokens`, `/admin/*`, `/setup`, `/error`, `/not-found`, `/access-denied` |
| Badges compatibles Codecov, inchangés | `/github/{owner}/{repo}/graph/badge.svg` et les autres formes d'ADR-014 |

| Forge | `{forge}` | Alias | `{owner}` |
|---|---|---|---|
| GitHub | `github` | `gh` | `owner` |
| Azure DevOps | `azure-devops` | | `org:project` |
| Bitbucket | `bitbucket` | `bb` | `workspace` |
| GitLab | `gitlab` | `gl` | `group:sub:sub2` |

## Inventaire des écrans

Les routes de `/orgs/{forge}/{org}` et `/admin/organizations` suivent `docs/PLAN.md`, ADR-0013 et ADR-014. `docs/ARCHITECTURE.md` §10bis.3 décrit encore `/admin/upload-tokens` et `/admin/repositories` jusqu'à M1.16a2.

### Navigation

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Navigation | Toutes les pages | M3.19 | connecté | Nominal |

### Accès et système

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Connexion | Connexion | M3.03 | public | Nominal, Refusé, Session expirée |
| Premier démarrage | `/setup` | M3.43 | jeton de mise en place | Nominal, Saisie manuelle, Terminé, Jeton invalide |
| Une erreur est survenue | `/error` | M0 | public | Nominal |
| Page introuvable | `/not-found` | M0 | public | Nominal |
| Accès refusé | `/access-denied` | M0 | public | Nominal |

### Dépôts

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Dépôts | `/` | M3.20 | connecté | Nominal, Aucun résultat, Vide, Chargement, Erreur |

### Dépôt

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Vue d’ensemble | `/{forge}/{owner}/{repo}` | M3.21, M3.22, M3.23 | lecture du dépôt | Nominal, Sans rapport, Chargement, Introuvable ou inaccessible |
| Commits | `/{forge}/{owner}/{repo}/commits` | M3.24 | lecture du dépôt | Nominal, Vide, Chargement, Introuvable ou inaccessible |
| Détail d’un commit | `/{forge}/{owner}/{repo}/commit/{sha}` | M3.25, M3.26, M3.27 | lecture du dépôt | Nominal, Erreur de configuration, Compacté, Chargement, Introuvable ou inaccessible |
| Rapport de couverture détaillé | `/{forge}/{owner}/{repo}/commit/{sha}/report` | M5.08, M5.15 | lecture du dépôt | Nominal, Traité avant le détail, Compacté, Chargement, Introuvable ou inaccessible |
| Source annotée | `/{forge}/{owner}/{repo}/commit/{sha}/blob/{**path}` | M3.29 | lecture du dépôt | Nominal, Depuis une PR, Source indisponible, Compacté, Chargement, Introuvable ou inaccessible |
| Pull requests | `/{forge}/{owner}/{repo}/pulls` | M3.30 | lecture du dépôt | Nominal, Vide, Chargement, Introuvable ou inaccessible |
| Comparaison de pull request | `/{forge}/{owner}/{repo}/pull/{n}` | M3.31, M3.32 | lecture du dépôt | Nominal, Diff incomplet, Base manquante, Comparaison en attente, Chargement, Introuvable ou inaccessible |
| Comparaison libre | `/{forge}/{owner}/{repo}/compare/{base}...{head}` | M3.33 | lecture du dépôt | Nominal, Plage invalide, Non calculée, Chargement, Introuvable ou inaccessible |
| Réglages du dépôt | `/{forge}/{owner}/{repo}/settings` | M3.34, M3.35 | admin du dépôt ou de l’organisation | Nominal, Inactif, YAML invalide, Dialogue : créer, Dialogue : valeur unique, Dialogue : importer, Dialogue : révoquer, Chargement, Accès refusé, Introuvable ou inaccessible |

### Mon compte

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Jetons API | `/account/tokens` | M3.36 | connecté | Nominal, Vide, Dialogue : créer, Dialogue : valeur unique, Dialogue : révoquer, Chargement, Erreur |

### Organisation

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Dépôts de l’organisation | `/orgs/{forge}/{org}/repositories` | M3.39 | admin de l’organisation | Nominal, Vide, Chargement, Erreur, Accès refusé, Introuvable ou inaccessible |
| Jetons d’upload de l’organisation | `/orgs/{forge}/{org}/upload-tokens` | M3.38 | admin de l’organisation | Nominal, Rotation conseillée, Vide, Dialogue : créer, Dialogue : valeur unique, Dialogue : importer, Dialogue : révoquer, Chargement, Accès refusé, Introuvable ou inaccessible |
| Paramètres de l’organisation | `/orgs/{forge}/{org}/settings` | M3.37a | admin de l’organisation | Nominal, Valeur invalide, Chargement, Accès refusé, Introuvable ou inaccessible |

### Documentation

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Documentation | `/docs/{page}` | M4.02n, M4.02o | public, y compris pendant le premier démarrage | Nominal, Non connecté, Navigation dépliée (mobile), Page inconnue, Chargement |

### Instance

| Écran | Route | Micro-feature | Accès | États dans la maquette |
|---|---|---|---|---|
| Organisations | `/admin/organizations` | M3.41a | admin d’instance | Nominal, Vide, Dialogue : admettre, Dialogue : suspendre, Chargement, Erreur, Accès refusé |
| Paramètres de l’instance | `/admin/settings` | M3.37 | admin d’instance | Nominal, Dialogue : remplacer un secret, Chargement, Erreur, Accès refusé |
| Opérations | `/admin/operations` | M3.40 | admin d’instance | Nominal, Vide, Chargement, Erreur, Accès refusé |
| Journal d’audit | `/admin/audit` | M3.41 | admin d’instance | Nominal, Vide, Chargement, Erreur, Accès refusé |

## Correspondance avec les micro-features

| Micro-feature | Écran validé | Ancre |
|---|---|---|
| M3.19 | Navigation | `#navigation` |
| M3.20 | Dépôts | `#home` |
| M3.21 | Vue d’ensemble | `#overview` |
| M3.22 | Vue d’ensemble | `#overview` |
| M3.23 | Vue d’ensemble | `#overview` |
| M3.24 | Commits | `#commits` |
| M3.25 | Détail d’un commit | `#commit` |
| M3.26 | Détail d’un commit | `#commit` |
| M3.27 | Détail d’un commit | `#commit` |
| M3.29 | Source annotée | `#source` |
| M3.30 | Pull requests | `#pulls` |
| M3.31 | Comparaison de pull request | `#pull` |
| M3.32 | Comparaison de pull request | `#pull` |
| M3.33 | Comparaison libre | `#compare` |
| M3.34 | Réglages du dépôt | `#repo-settings` |
| M3.35 | Réglages du dépôt | `#repo-settings` |
| M3.36 | Jetons API | `#account-tokens` |
| M3.37 | Paramètres de l’instance | `#admin-settings` |
| M3.37a | Paramètres de l’organisation | `#org-settings` |
| M3.38 | Jetons d’upload de l’organisation | `#org-upload-tokens` |
| M3.39 | Dépôts de l’organisation | `#org-repositories` |
| M3.40 | Opérations | `#admin-operations` |
| M3.41 | Journal d’audit | `#admin-audit` |
| M3.41a | Organisations | `#admin-organizations` |
| M3.43 | Premier démarrage | `#setup` |
| M4.02n, M4.02o | Documentation | `#docs` |
| M5.08, M5.15 | Rapport de couverture détaillé | `#commitreport` |

M3.28 et M3.42 sont des micro-features sans écran (backend de la source annotée et de `/setup`), et M3.43a ne contient que des tests.

## Composants

Le CSS de la maquette reprend `tokens.css`, `components.css` et `app.css`, avec leurs classes `cc-*`. Les classes `cc-mock-*` sont la barre de revue et ne sont jamais livrées.

| Situation | Composants ou classes |
|---|---|
| Existants (`Web/Components/Ui`) | `CcAppShell`, `CcTopNav`, `CcUserMenu`, `CcPageContainer`, `CcHeader`, `CcPillTabs`, `CcFolderTabs`, `CcKpiCard`, `CcBadge`, `CcButton`, `CcBanner`, `CcEmptyState`, `CcErrorBoundary`, `CcLoadingSpinner`, `CcToast`, `CcSearchBar`, `CcSelectField`, `CcTextField`, `CcIcon`, `CcTable`, `CcDialog`, `CcSwitch`, `CcPager` (M3.24), `CcLineChart` (M3.22), `CcSparkline` (M3.20), `CcCoverageBar` (M3.20), `CcGauge` (M3.40) |
| Nommés par le plan, à construire | aucun ; `CcBarChart` n'est employé par aucun écran validé |
| Besoins nouveaux, avec leurs classes | étapes de `/setup` (`cc-stepper`, M3.43) ; source annotée (`cc-source`, `cc-panel__stats`, M3.29) ; code et copie (`cc-code`, `cc-copy`, M3.23) ; documentation (`cc-docs`, `cc-md`, `cc-callout`, `cc-help`, `cc-help-link`, `cc-code__tools`, `cc-skeleton-line`, M4.02o) |

### À reprendre lors de la construction des pages

- **M3.22 :** `--cc-chart-1` et `--cc-chart-2` sont presque identiques pour un lecteur daltonien (`ΔE` 1,0 en protanopie). Les séries gardent donc un pointillé et une étiquette directe, en plus de la couleur.

## Documentation dans l'application

Maquette **M4.02n**, validée par le propriétaire le 2026-11-15 : écran `#docs` et ses états, entrée de la barre du haut, boutons « ? » des écrans listés ci-dessous.

### Source et langue

- La documentation est le Markdown de `docs/` embarqué dans le Web et rendu côté serveur : celle de la **version déployée**, affichée dans la navigation (`v0.1.0-beta.2`) et sous le titre de chaque page avec son fichier source.
- Seule la liste blanche de M4.02o est publiée ; le plan, les ADR, `ARCHITECTURE.md` et `CONVENTIONS.md` jamais.
- Le contenu est dans la langue de la console quand sa traduction `{nom}.{langue}.md` existe (M4.02v, règles de `docs/CONVENTIONS.md` §11.7), sinon en **anglais**, la langue de référence, avec le bandeau de « Documentation non traduite ». L'habillage (« Sur cette page », « Précédent », « Suivant », la version, les états) suit toujours la langue de la console ; la navigation et les titres des pages suivent la langue servie de chaque document.

### Accès et entrée

| Situation | Entrée « Documentation » |
|---|---|
| Connecté, à partir de 1181 px | Bouton rond `book` dans `cc-topnav__icons`, à gauche du thème ; plein (`aria-current="page"`) sur `/docs` |
| Connecté, de 1041 à 1180 px | Le bouton rond est masqué ; l'entrée reste dans le menu utilisateur, entre « Mon compte » et « Se déconnecter » |
| Connecté, jusqu'à 1040 px | Dernière ligne du menu mobile, sous un filet, avec l'icône `book` |
| Connecté, toutes largeurs | Toujours aussi dans le menu utilisateur |
| Non connecté ou pendant `/setup` | Lien « Lire la documentation » de la carte de connexion ; `/docs` s'ouvre dans une barre publique (marque, Documentation, thème, Se connecter) |

Un sixième onglet texte n'entre pas : avec le sélecteur d'organisation, la barre déborde déjà de 21 px à 1041 px avant M4.02h. Le bouton rond prend 48 px ; il n'apparaît qu'à partir de 1181 px, et le nom de l'utilisateur se masque jusqu'à 1399 px (au lieu de 1320 px) pour garder au moins 18 px entre le sélecteur et les onglets. Les règles existantes de la barre ne changent pas.

### Page `/docs/{page}`

- **Trois colonnes** à partir de 1281 px : navigation (250 px, collante), article (`CcPanel`), sommaire « Sur cette page » (210 px, collant) tiré des titres `h2`. De 901 à 1280 px, le sommaire passe en tête de l'article, replié (`details`). Jusqu'à 900 px, la navigation se replie derrière un bouton qui affiche la rubrique et la page courantes.
- **Navigation** par rubrique, dans cet ordre : Getting started ; GitHub App and accounts ; Configure a repository (GitHub Actions, Azure Pipelines, Upload tokens, `codecov.yml`) ; Settings ; Operations ; Migration from Codecov. Les intitulés sont les titres des documents, en anglais. M4.02o n'en garde qu'une page par document (« Écarts de M4.02o avec la maquette »).
- **Article** : fil d'Ariane, titre, version et fichier source, chapeau, contenu, puis liens précédent et suivant dans l'ordre de la navigation.
- **Rendu du Markdown** (`cc-md`) : `h2` et `h3` avec ancre `#` au survol ; tableaux en `CcTable` ; blocs de code en `cc-copy` avec la langue et le bouton Copier (`cc-code__tools`) ; encadrés `cc-callout--note`, `--tip` et `--warning` rendus depuis les alertes GitHub `> [!NOTE]`, `> [!TIP]`, `> [!WARNING]` ; liens entre documents réécrits vers `/docs/…`.
- **États** : page inconnue (`CcEmptyState` dans l'article, navigation conservée, réponse 404) ; chargement (titre connu, `CcLoadingSpinner` et lignes `cc-skeleton-line`) ; mobile avec la navigation repliée ou dépliée.

### Aide contextuelle

Un bouton `cc-help` « ? » (26 px, bordure fine, à droite du titre de `CcHeader`, paramètre `help`) ouvre la page et l'ancre de la documentation. Son nom accessible est « Aide sur cette page : {titre de la page} ». Dans un état vide, l'aide est un lien texte `cc-help-link` avec l'icône `book`.

| Écran | Cible de la maquette | Cible construite (M4.02o) |
|---|---|---|
| `/setup` | `/docs/getting-started#open-the-setup-page` (barre publique) | `/docs/operations#first-deployment` |
| `/admin/organizations` | `/docs/onboarding#admit-it` | `/docs/organization-onboarding#2-admit-it` |
| `/admin/settings` | `/docs/settings#instance-settings` | aucune, faute de document |
| `/orgs/{forge}/{org}/settings` | `/docs/settings#organization-settings` | `/docs/organization-onboarding#3-configure-it` |
| `/orgs/{forge}/{org}/upload-tokens` | `/docs/upload-tokens` | `/docs/migration-from-codecov#2-tokens` |
| Dépôt sans rapport (`#overview--nodata`) | « Comment envoyer le premier rapport » → `/docs/github-actions` | `/docs/github-actions` |

Chaque cible doit exister : M4.02o le vérifie par un contrôle de source (`DocumentationSourceTests`).

### Écarts de M4.02o avec la maquette

Décision du propriétaire du 2026-11-15 (option A) :

- **Une page par document** de la liste blanche, avec ses vrais titres, au lieu des treize pages de la maquette taillées dans ces documents avec des titres inventés. Pages et rubriques : Getting started (`operations`) ; GitHub App and accounts (`organization-onboarding`, `github-checklist`) ; Configure a repository (`github-actions`) ; Operations (`deployment-azure`) ; Migration from Codecov (`migration-from-codecov`). La rubrique Settings, sans document, n'apparaît pas.
- **Aides** sur les titres réels, comme dans la troisième colonne du tableau ci-dessus ; pas de « ? » sur `/admin/settings`.
- **Documents manquants**, à écrire dans une micro-feature suivante : les paramètres de l'instance, et la page du template Azure Pipelines (`pipelines/templates/README.md`).
- **Barre publique** : `/docs` garde la barre de la console ; non connecté, elle ne montre que la marque, Documentation (`cc-topnav__docs--public`), le thème et Se connecter.

## Langues de la console

Maquette **M4.02q**, validée par le propriétaire le 2026-11-15. La décision et ce qui reste en anglais sont dans l'ADR de M4.02r.

### Langues

| Code | Nom affiché | Rôle |
|---|---|---|
| `en` | English | Référence, langue par défaut et de repli |
| `fr` | Français | Traduction |
| `it` | Italiano | Traduction |
| `es` | Español | Traduction |

Chaque langue est nommée dans sa propre langue, quel que soit l'affichage courant, avec `lang` et `hreflang` sur son lien. La langue vient du cookie de culture posé par le sélecteur, sinon de `Accept-Language`, sinon de l'anglais ; une variante régionale du navigateur (`it-CH`, `es-MX`) retombe sur sa langue.

### Sélecteur `CcLanguagePicker`

| Situation | Emplacement |
|---|---|
| Connecté, à partir de 1041 px | En bas du menu utilisateur, sous un filet : libellé « Langue » avec l'icône `globe`, puis les quatre langues en deux colonnes |
| Connecté, jusqu'à 1040 px | Dernière ligne du menu mobile, sous Documentation et un filet, sur quatre colonnes (deux sous 400 px) |
| Non connecté, `/docs` | Bouton rond `globe` dans `cc-topnav__icons` de la barre publique, entre Documentation et le thème, avec le code de la langue en pastille (`cc-lang-menu__code`) ; il ouvre le même groupe |
| Connexion et `/setup` | Ligne `cc-lang--inline` en bas de la carte de connexion et sous les panneaux de `/setup` |

- La langue courante est en évidence (`aria-current="true"`, fond `--cc-accent-soft`).
- Aucun bouton n'entre dans la barre du haut connectée : sa largeur est réglée au pixel près depuis M4.02h.
- Un choix est un lien vers `/culture?lang={code}&returnUrl={page courante}`, qui pose le cookie et revient sur la page, rechargée (un circuit Blazor garde sa culture de départ) ; aucun dialogue ni confirmation.

### Documentation non traduite

Sans `{nom}.{langue}.md` pour la langue de la console, la page anglaise s'affiche avec un bandeau `CcBanner` d'information au-dessus du contenu, par exemple « Cette page n'est pas encore traduite en français : elle s'affiche en anglais, la langue de référence de la documentation. », et le contenu porte `lang="en"`. En anglais, jamais de bandeau. Les ancres sont celles de la page anglaise dans toutes les langues : une aide contextuelle ou un lien partagé ouvre la même section quelle que soit la langue.

### Formats

Nombres, pourcentages et dates suivent la culture : `74,2 %` en français et en espagnol, `74,2%` en italien, `74.2%` en anglais. Les attributs numériques des SVG restent en culture invariante (M4.02h).

## Rapport de couverture détaillé

Maquette **M5.08**, validée par le propriétaire le 2026-11-15 ; décision et stockage dans [ADR-016](adr/0016-detailed-coverage.md).

- **Entrée** : bouton `pill` « Rapport détaillé » (icône `chart`) dans les outils du panneau Fichiers du détail d'un commit, avant la recherche ; l'arbre Codecov du panneau ne change pas.
- **Tête** : fil d'Ariane jusqu'au commit, liste des flags dans les actions (même vue par flag que le panneau Fichiers, ADR-008).
- **Quatre `CcStatCard`** : Couverture (Codecov), en accent, toujours le chiffre de tête ; Lignes (exécutées au moins une fois, partielles comprises) ; Branches ; Méthodes. Chaque carte donne couvert et couvrable dans son pied.
- **Tableau plat** (pas d'arbre, comme ReportGenerator), toutes les lignes sans pagination : Fichier, Codecov, puis trois groupes Lignes, Branches et Méthodes de deux colonnes (« Couv. / total » et `%` avec une `CcCoverageBar` couvert/non couvert). Une ligne Total de tous les fichiers de la vue (flag compris) reste en tête, sans tri ni filtre.
- **Tri** : un clic sur un en-tête (Fichier, Codecov, chaque `%`) trie, un second inverse ; `aria-sort` sur l'en-tête. Tri par défaut : Codecov croissant. Les valeurs `—` restent en fin de liste dans les deux sens.
- **Filtre** : recherche sur le chemin et liste « Afficher » : tous les fichiers, sous l'objectif du statut projet (80 % dans la maquette), avec des branches non couvertes, avec des méthodes non exécutées. Sans résultat : état vide « Aucun fichier ne correspond ».
- **`—`** : rien à couvrir, ou le format ne donne pas la donnée (branches d'un fichier `.js` en LCOV, avec une infobulle) ; jamais 0 pour une donnée inconnue.
- **Traité avant le détail** : index `CFI1`, bandeau d'information avec « Retraiter » pour un admin du dépôt, `—` dans les cartes et les colonnes de détail ; seul Codecov est rempli.
- **Mobile (≤ 640 px)** : le tableau devient une liste de cartes (chemin, puis Codecov, Lignes, Branches, Méthodes, chacun avec sa barre) ; une liste « Trier » remplace les en-têtes.

## Libellés clés

Les clés suivent `Common.resx` (anglais) et ses traductions `Common.fr.resx`, `Common.it.resx` et `Common.es.resx`. Le nom du produit vient de `UiStrings.resx`.

| Clé | Anglais | Français | Italien | Espagnol |
|---|---|---|---|---|
| `ProductName` | Coverage | Coverage | Coverage | Coverage |
| `Nav_Repositories` | Repositories | Dépôts | Repository | Repositorios |
| `Nav_Organization` | Organization | Organisation | Organizzazione | Organización |
| `Nav_Instance` | Instance | Instance | Istanza | Instancia |
| `Nav_Account` | My account | Mon compte | Il mio account | Mi cuenta |
| `Nav_SignOut` | Sign out | Se déconnecter | Esci | Cerrar sesión |
| `Nav_Breadcrumb` | Breadcrumb | Fil d’Ariane | Percorso di navigazione | Ruta de navegación |
| `Nav_Main` | Main navigation | Navigation principale | Navigazione principale | Navegación principal |
| `Nav_Docs` | Documentation | Documentation | Documentazione | Documentación |
| `Action_Help` | Help on this page | Aide sur cette page | Aiuto su questa pagina | Ayuda sobre esta página |
| `Org_Switcher` | Organization | Organisation | Organizzazione | Organización |
| `Org_All` | All organizations | Toutes les organisations | Tutte le organizzazioni | Todas las organizaciones |
| `Forge_GitHub` | GitHub | GitHub | GitHub | GitHub |
| `Forge_AzureDevOps` | Azure DevOps | Azure DevOps | Azure DevOps | Azure DevOps |
| `Forge_Bitbucket` | Bitbucket | Bitbucket | Bitbucket | Bitbucket |
| `Forge_GitLab` | GitLab | GitLab | GitLab | GitLab |
| `Tab_Overview` | Overview | Vue d’ensemble | Panoramica | Resumen |
| `Tab_Commits` | Commits | Commits | Commit | Commits |
| `Tab_Pulls` | Pull requests | Pull requests | Pull request | Pull requests |
| `Tab_RepoSettings` | Settings | Réglages | Impostazioni | Configuración |
| `Tab_OrgRepositories` | Repositories | Dépôts | Repository | Repositorios |
| `Tab_UploadTokens` | Upload tokens | Jetons d’upload | Token di upload | Tokens de upload |
| `Tab_OrgSettings` | Settings | Paramètres | Impostazioni | Configuración |
| `Tab_Organizations` | Organizations | Organisations | Organizzazioni | Organizaciones |
| `Tab_InstanceSettings` | Settings | Paramètres | Impostazioni | Configuración |
| `Tab_Operations` | Operations | Opérations | Operazioni | Operaciones |
| `Tab_Audit` | Audit log | Journal d’audit | Registro di audit | Registro de auditoría |
| `Role_Participant` | Participant | Participant | Partecipante | Participante |
| `Role_OrganizationAdmin` | Organization admin | Administrateur d’organisation | Amministratore dell’organizzazione | Administrador de la organización |
| `Role_InstanceAdmin` | Instance admin | Administrateur d’instance | Amministratore dell’istanza | Administrador de la instancia |
| `OrganizationState_Pending` | Pending | En attente | In attesa | Pendiente |
| `OrganizationState_Active` | Active | Active | Attiva | Activa |
| `OrganizationState_Suspended` | Suspended | Suspendue | Sospesa | Suspendida |
| `SuspensionReason_Administrator` | By an instance admin | Par un administrateur d’instance | Da un amministratore dell’istanza | Por un administrador de la instancia |
| `SuspensionReason_AppUninstalled` | GitHub App uninstalled | GitHub App désinstallée | GitHub App disinstallata | GitHub App desinstalada |
| `AccountType_Organization` | Organization | Organisation | Organizzazione | Organización |
| `AccountType_User` | Personal account | Compte personnel | Account personale | Cuenta personal |
| `Repository_Active` | Active | Actif | Attivo | Activo |
| `Repository_Inactive` | Inactive | Inactif | Inattivo | Inactivo |
| `Commit_Complete` | Complete | Terminé | Completato | Completado |
| `Commit_Pending` | Processing | En cours | In elaborazione | En curso |
| `Commit_Error` | Error | Erreur | Errore | Error |
| `Session_CarriedForward` | Carried forward | Reporté | Riportato | Arrastrado |
| `Upload_Failed` | Failed | Échoué | Fallito | Fallido |
| `Pull_Open` | Open | Ouverte | Aperta | Abierta |
| `Pull_Merged` | Merged | Fusionnée | Unita | Fusionada |
| `Pull_Closed` | Closed | Fermée | Chiusa | Cerrada |
| `Status_Success` | Success | Succès | Riuscito | Éxito |
| `Status_Failure` | Failure | Échec | Fallito | Fallo |
| `Setting_SourceDatabase` | Database | Base | Database | Base de datos |
| `Setting_SourceEnvironment` | Environment | Environnement | Ambiente | Entorno |
| `Setting_SourceDefault` | Default | Défaut | Predefinito | Predeterminado |
| `Setting_SourceOrganization` | Organization | Organisation | Organizzazione | Organización |
| `Setting_RequiresRestart` | Restart required | Redémarrage requis | Riavvio necessario | Requiere reinicio |
| `Setting_Defined` | Set | Défini | Definita | Definido |
| `Setting_NotDefined` | Not set | Non défini | Non definita | No definido |
| `Action_Save` | Save | Enregistrer | Salva | Guardar |
| `Action_Cancel` | Cancel | Annuler | Annulla | Cancelar |
| `Action_Close` | Close | Fermer | Chiudi | Cerrar |
| `Action_Copy` | Copy | Copier | Copia | Copiar |
| `Action_Create` | Create a token | Créer un jeton | Crea un token | Crear un token |
| `Action_Import` | Import a CODECOV_TOKEN | Importer un CODECOV_TOKEN | Importa un CODECOV_TOKEN | Importar un CODECOV_TOKEN |
| `Action_Revoke` | Revoke | Révoquer | Revoca | Revocar |
| `Action_Renew` | Renew | Renouveler | Rinnova | Renovar |
| `Action_Replace` | Replace | Remplacer | Sostituisci | Reemplazar |
| `Action_Reset` | Reset | Réinitialiser | Reimposta | Restablecer |
| `Action_Retry` | Retry | Relancer | Riprova | Reintentar |
| `Action_Renotify` | Notify again | Re-notifier | Notifica di nuovo | Volver a notificar |
| `Action_Reprocess` | Reprocess | Retraiter | Rielabora | Volver a procesar |
| `Action_Sync` | Sync with GitHub | Synchroniser avec GitHub | Sincronizza con GitHub | Sincronizar con GitHub |
| `Action_Admit` | Admit | Admettre | Ammetti | Admitir |
| `Action_AdmitAccount` | Admit an account | Admettre un compte | Ammetti un account | Admitir una cuenta |
| `Action_Suspend` | Suspend | Suspendre | Sospendi | Suspender |
| `Action_NextUncovered` | Next uncovered line | Prochaine ligne non couverte | Prossima riga non coperta | Siguiente línea no cubierta |
| `Action_SignIn` | Sign in with GitHub | Se connecter avec GitHub | Accedi con GitHub | Iniciar sesión con GitHub |
| `Action_Previous` | Previous | Précédent | Precedente | Anterior |
| `Action_Next` | Next | Suivant | Successivo | Siguiente |
| `Filter_All` | All | Tous | Tutti | Todos |
| `Filter_Active` | Active | Actifs | Attivi | Activos |
| `Filter_Inactive` | Inactive | Inactifs | Inattivi | Inactivos |
| `Filter_AllFlags` | All flags | Tous les flags | Tutti i flag | Todos los flags |
| `Search_Repositories` | Search repositories | Rechercher un dépôt | Cerca repository | Buscar repositorios |
| `Search_Files` | Filter files | Filtrer les fichiers | Filtra i file | Filtrar archivos |
| `Column_Repository` | Repository | Dépôt | Repository | Repositorio |
| `Column_Coverage` | Coverage | Couverture | Copertura | Cobertura |
| `Column_LastCommit` | Last commit | Dernier commit | Ultimo commit | Último commit |
| `Column_LastUpload` | Last upload | Dernier upload | Ultimo upload | Último upload |
| `Column_Flags` | Flags | Flags | Flag | Flags |
| `Column_Commit` | Commit | Commit | Commit | Commit |
| `Column_Author` | Author | Auteur | Autore | Autor |
| `Column_Date` | Date | Date | Data | Fecha |
| `Column_State` | State | État | Stato | Estado |
| `Column_Sessions` | Sessions | Sessions | Sessioni | Sesiones |
| `Column_Patch` | Patch | Patch | Patch | Patch |
| `Column_File` | File | Fichier | File | Archivo |
| `Column_Lines` | Lines | Lignes | Righe | Líneas |
| `Column_Hits` | Hits | Couvertes | Coperte | Cubiertas |
| `Column_Partials` | Partials | Partielles | Parziali | Parciales |
| `Column_Misses` | Misses | Manquées | Mancate | No cubiertas |
| `Column_ProjectDelta` | Project Δ | Δ projet | Δ progetto | Δ proyecto |
| `Column_Name` | Name | Nom | Nome | Nombre |
| `Column_Hint` | Hint | Indice | Indizio | Pista |
| `Column_Created` | Created | Créé | Creato | Creado |
| `Column_LastUsed` | Last used | Dernière utilisation | Ultimo utilizzo | Último uso |
| `Column_Organization` | Organization | Organisation | Organizzazione | Organización |
| `Column_Forge` | Forge | Forge | Forge | Forja |
| `Column_Type` | Type | Type | Tipo | Tipo |
| `Column_Reason` | Reason | Motif | Motivo | Motivo |
| `Column_AdmittedBy` | Admitted by | Admise par | Ammessa da | Admitida por |
| `Column_QueueDepth` | Jobs in queue | Jobs en file | Job in coda | Jobs en cola |
| `Inaccessible_Title` | Not found or inaccessible | Introuvable ou inaccessible | Non trovato o inaccessibile | No encontrado o inaccesible |
| `Inaccessible_Description` | This address does not exist, or your GitHub account cannot read it. | Cette adresse n’existe pas, ou votre compte GitHub ne peut pas la lire. | Questo indirizzo non esiste, oppure il tuo account GitHub non può leggerlo. | Esta dirección no existe o su cuenta de GitHub no puede leerla. |
| `Loading` | Loading | Chargement | Caricamento | Cargando |
| `Retry` | Try again | Réessayer | Riprova | Volver a intentarlo |
| `AccessDenied_Title` | Access denied | Accès refusé | Accesso negato | Acceso denegado |
| `AccessDenied_Description` | You do not have the permission to open this page. Ask an administrator if you need it. | Vous n’avez pas la permission d’ouvrir cette page. Demandez-la à un administrateur si vous en avez besoin. | Non hai il permesso di aprire questa pagina. Chiedilo a un amministratore se ti serve. | No tiene permiso para abrir esta página. Solicítelo a un administrador si lo necesita. |
| `NotFound_Title` | Page not found | Page introuvable | Pagina non trovata | Página no encontrada |
| `NotFound_Description` | The address matches no page. Check the link, or start again from the repositories. | Cette adresse ne correspond à aucune page. Vérifiez le lien, ou repartez de la liste des dépôts. | L’indirizzo non corrisponde ad alcuna pagina. Controlla il link, oppure riparti dall’elenco dei repository. | La dirección no corresponde a ninguna página. Compruebe el enlace o vuelva a empezar desde los repositorios. |
| `Error_Title` | An error occurred | Une erreur est survenue | Si è verificato un errore | Se ha producido un error |
| `Error_Description` | The page could not be displayed. Try again in a moment; if the problem persists, give the request identifier below to an administrator. | La page n’a pas pu être affichée. Réessayez dans un instant ; si le problème persiste, transmettez l’identifiant de requête ci-dessous à un administrateur. | Non è stato possibile visualizzare la pagina. Riprova tra un momento; se il problema persiste, comunica a un amministratore l’identificativo della richiesta riportato qui sotto. | No se pudo mostrar la página. Inténtelo de nuevo en un momento; si el problema persiste, facilite a un administrador el identificador de solicitud que aparece abajo. |
| `Error_RequestId` | Request identifier | Identifiant de requête | Identificativo della richiesta | Identificador de solicitud |
| `BackHome` | Back to the repositories | Retour aux dépôts | Torna ai repository | Volver a los repositorios |
| `Access.NoRepository` | Your GitHub account has access to no repository of an admitted organization. | Votre compte GitHub n’a accès à aucun dépôt d’une organisation admise. | Il tuo account GitHub non ha accesso ad alcun repository di un’organizzazione ammessa. | Su cuenta de GitHub no tiene acceso a ningún repositorio de una organización admitida. |
