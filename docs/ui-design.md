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
| Détail d’un commit | `/{forge}/{owner}/{repo}/commit/{sha}` | M3.25, M3.26, M3.27 | lecture du dépôt | Nominal, Erreur de configuration, Chargement, Introuvable ou inaccessible |
| Source annotée | `/{forge}/{owner}/{repo}/commit/{sha}/blob/{**path}` | M3.29 | lecture du dépôt | Nominal, Depuis une PR, Source indisponible, Chargement, Introuvable ou inaccessible |
| Pull requests | `/{forge}/{owner}/{repo}/pulls` | M3.30 | lecture du dépôt | Nominal, Vide, Chargement, Introuvable ou inaccessible |
| Comparaison de pull request | `/{forge}/{owner}/{repo}/pull/{n}` | M3.31, M3.32 | lecture du dépôt | Nominal, Diff incomplet, Base manquante, Chargement, Introuvable ou inaccessible |
| Comparaison libre | `/{forge}/{owner}/{repo}/compare/{base}...{head}` | M3.33 | lecture du dépôt | Nominal, Plage invalide, Chargement, Introuvable ou inaccessible |
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

M3.28 et M3.42 sont des micro-features sans écran (backend de la source annotée et de `/setup`), et M3.43a ne contient que des tests.

## Composants

Le CSS de la maquette reprend `tokens.css`, `components.css` et `app.css`, avec leurs classes `cc-*`. Les classes `cc-mock-*` sont la barre de revue et ne sont jamais livrées.

| Situation | Composants ou classes |
|---|---|
| Existants (`Web/Components/Ui`) | `CcAppShell`, `CcTopNav`, `CcUserMenu`, `CcPageContainer`, `CcHeader`, `CcPillTabs`, `CcFolderTabs`, `CcKpiCard`, `CcBadge`, `CcButton`, `CcBanner`, `CcEmptyState`, `CcErrorBoundary`, `CcLoadingSpinner`, `CcToast`, `CcSearchBar`, `CcSelectField`, `CcTextField`, `CcIcon` |
| Nommés par le plan, à construire | `CcStatCard` (M3.21), `CcLineChart` (M3.22), `CcGauge` (couverture du patch et quota GitHub) ; `CcBarChart` n'est employé par aucun écran validé |
| Besoins nouveaux, avec leurs classes | emplacement du sélecteur d'organisation dans `CcTopNav` (`cc-topnav__start`, `cc-topnav__org`) ; tableau (`cc-table`, `cc-table-wrap`) ; pagination (`cc-panel__foot`) ; dialogue (`cc-dialog`) pour `IssuedTokenDialog` et `SecretReplaceDialog` ; interrupteur (`cc-switch`) ; ligne de paramètre (`cc-setting`) pour `SettingEditor` ; étapes de `/setup` (`cc-stepper`) ; source annotée (`cc-source`) ; code et copie (`cc-code`, `cc-copy`) ; barre de couverture (`cc-covbar`) ; sparkline (`cc-spark`) ; petites statistiques (`cc-mini`, `cc-panel__stats`) ; liste et clé-valeur (`cc-list`, `cc-kv`) ; page de connexion (`cc-signin`) |

### À reprendre lors de la construction des pages

- **M3.19 :** dans `components.css`, `.cc-round-button` vient après `.cc-topnav__menu-button` et annule son `display: none`, si bien que le bouton de menu mobile reste visible sur bureau. La maquette le masque par une règle propre.
- **M3.41a :** aucun libellé n'est prévu pour `SuspensionReason.ReservedLogin`, que M3.43a supprime ; aucune organisation réelle n'existe avant le pilote.
- **M3.22 :** `--cc-chart-1` et `--cc-chart-2` sont presque identiques pour un lecteur daltonien (`ΔE` 1,0 en protanopie). Les séries gardent donc un pointillé et une étiquette directe, en plus de la couleur.

## Libellés clés

Les clés suivent `Common.resx` (anglais) et `Common.fr.resx` (français). Le nom du produit vient de `UiStrings.resx`.

| Clé | Anglais | Français |
|---|---|---|
| `ProductName` | Coverage | Coverage |
| `Nav_Repositories` | Repositories | Dépôts |
| `Nav_Organization` | Organization | Organisation |
| `Nav_Instance` | Instance | Instance |
| `Nav_Account` | My account | Mon compte |
| `Nav_SignOut` | Sign out | Se déconnecter |
| `Nav_Breadcrumb` | Breadcrumb | Fil d’Ariane |
| `Nav_Main` | Main navigation | Navigation principale |
| `Org_Switcher` | Organization | Organisation |
| `Org_All` | All organizations | Toutes les organisations |
| `Forge_GitHub` | GitHub | GitHub |
| `Forge_AzureDevOps` | Azure DevOps | Azure DevOps |
| `Forge_Bitbucket` | Bitbucket | Bitbucket |
| `Forge_GitLab` | GitLab | GitLab |
| `Tab_Overview` | Overview | Vue d’ensemble |
| `Tab_Commits` | Commits | Commits |
| `Tab_Pulls` | Pull requests | Pull requests |
| `Tab_RepoSettings` | Settings | Réglages |
| `Tab_OrgRepositories` | Repositories | Dépôts |
| `Tab_UploadTokens` | Upload tokens | Jetons d’upload |
| `Tab_OrgSettings` | Settings | Paramètres |
| `Tab_Organizations` | Organizations | Organisations |
| `Tab_InstanceSettings` | Settings | Paramètres |
| `Tab_Operations` | Operations | Opérations |
| `Tab_Audit` | Audit log | Journal d’audit |
| `Role_Participant` | Participant | Participant |
| `Role_OrganizationAdmin` | Organization admin | Administrateur d’organisation |
| `Role_InstanceAdmin` | Instance admin | Administrateur d’instance |
| `OrganizationState_Pending` | Pending | En attente |
| `OrganizationState_Active` | Active | Active |
| `OrganizationState_Suspended` | Suspended | Suspendue |
| `SuspensionReason_Administrator` | By an instance admin | Par un administrateur d’instance |
| `SuspensionReason_AppUninstalled` | GitHub App uninstalled | GitHub App désinstallée |
| `AccountType_Organization` | Organization | Organisation |
| `AccountType_User` | Personal account | Compte personnel |
| `Repository_Active` | Active | Actif |
| `Repository_Inactive` | Inactive | Inactif |
| `Commit_Complete` | Complete | Terminé |
| `Commit_Pending` | Processing | En cours |
| `Commit_Error` | Error | Erreur |
| `Session_CarriedForward` | Carried forward | Reporté |
| `Upload_Failed` | Failed | Échoué |
| `Pull_Open` | Open | Ouverte |
| `Pull_Merged` | Merged | Fusionnée |
| `Pull_Closed` | Closed | Fermée |
| `Status_Success` | Success | Succès |
| `Status_Failure` | Failure | Échec |
| `Setting_SourceDatabase` | Database | Base |
| `Setting_SourceEnvironment` | Environment | Environnement |
| `Setting_SourceDefault` | Default | Défaut |
| `Setting_SourceOrganization` | Organization | Organisation |
| `Setting_RequiresRestart` | Restart required | Redémarrage requis |
| `Setting_Defined` | Set | Défini |
| `Setting_NotDefined` | Not set | Non défini |
| `Action_Save` | Save | Enregistrer |
| `Action_Cancel` | Cancel | Annuler |
| `Action_Close` | Close | Fermer |
| `Action_Copy` | Copy | Copier |
| `Action_Create` | Create a token | Créer un jeton |
| `Action_Import` | Import a CODECOV_TOKEN | Importer un CODECOV_TOKEN |
| `Action_Revoke` | Revoke | Révoquer |
| `Action_Renew` | Renew | Renouveler |
| `Action_Replace` | Replace | Remplacer |
| `Action_Reset` | Reset | Réinitialiser |
| `Action_Retry` | Retry | Relancer |
| `Action_Renotify` | Notify again | Re-notifier |
| `Action_Reprocess` | Reprocess | Retraiter |
| `Action_Sync` | Sync with GitHub | Synchroniser avec GitHub |
| `Action_Admit` | Admit | Admettre |
| `Action_AdmitAccount` | Admit an account | Admettre un compte |
| `Action_Suspend` | Suspend | Suspendre |
| `Action_NextUncovered` | Next uncovered line | Prochaine ligne non couverte |
| `Action_SignIn` | Sign in with GitHub | Se connecter avec GitHub |
| `Action_Previous` | Previous | Précédent |
| `Action_Next` | Next | Suivant |
| `Filter_All` | All | Tous |
| `Filter_Active` | Active | Actifs |
| `Filter_Inactive` | Inactive | Inactifs |
| `Filter_AllFlags` | All flags | Tous les flags |
| `Search_Repositories` | Search repositories | Rechercher un dépôt |
| `Search_Files` | Filter files | Filtrer les fichiers |
| `Column_Repository` | Repository | Dépôt |
| `Column_Coverage` | Coverage | Couverture |
| `Column_LastCommit` | Last commit | Dernier commit |
| `Column_LastUpload` | Last upload | Dernier upload |
| `Column_Flags` | Flags | Flags |
| `Column_Commit` | Commit | Commit |
| `Column_Author` | Author | Auteur |
| `Column_Date` | Date | Date |
| `Column_State` | State | État |
| `Column_Sessions` | Sessions | Sessions |
| `Column_Patch` | Patch | Patch |
| `Column_File` | File | Fichier |
| `Column_Lines` | Lines | Lignes |
| `Column_Hits` | Hits | Couvertes |
| `Column_Partials` | Partials | Partielles |
| `Column_Misses` | Misses | Manquées |
| `Column_ProjectDelta` | Project Δ | Δ projet |
| `Column_Name` | Name | Nom |
| `Column_Hint` | Hint | Indice |
| `Column_Created` | Created | Créé |
| `Column_LastUsed` | Last used | Dernière utilisation |
| `Column_Organization` | Organization | Organisation |
| `Column_Forge` | Forge | Forge |
| `Column_Type` | Type | Type |
| `Column_Reason` | Reason | Motif |
| `Column_AdmittedBy` | Admitted by | Admise par |
| `Column_QueueDepth` | Jobs in queue | Jobs en file |
| `Inaccessible_Title` | Not found or inaccessible | Introuvable ou inaccessible |
| `Inaccessible_Description` | This address does not exist, or your GitHub account cannot read it. | Cette adresse n’existe pas, ou votre compte GitHub ne peut pas la lire. |
| `Loading` | Loading | Chargement |
| `Retry` | Try again | Réessayer |
| `AccessDenied_Title` | Access denied | Accès refusé |
| `AccessDenied_Description` | You do not have the permission to open this page. Ask an administrator if you need it. | Vous n’avez pas la permission d’ouvrir cette page. Demandez-la à un administrateur si vous en avez besoin. |
| `NotFound_Title` | Page not found | Page introuvable |
| `NotFound_Description` | The address matches no page. Check the link, or start again from the repositories. | Cette adresse ne correspond à aucune page. Vérifiez le lien, ou repartez de la liste des dépôts. |
| `Error_Title` | An error occurred | Une erreur est survenue |
| `Error_Description` | The page could not be displayed. Try again in a moment; if the problem persists, give the request identifier below to an administrator. | La page n’a pas pu être affichée. Réessayez dans un instant ; si le problème persiste, transmettez l’identifiant de requête ci-dessous à un administrateur. |
| `Error_RequestId` | Request identifier | Identifiant de requête |
| `BackHome` | Back to the repositories | Retour aux dépôts |
| `Access.NoRepository` | Your GitHub account has access to no repository of an admitted organization. | Votre compte GitHub n’a accès à aucun dépôt d’une organisation admise. |
