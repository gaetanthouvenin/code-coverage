# ADR-015 : Langues de la console et ce qui reste en anglais

- **Statut :** proposé (M4.02r), à accepter par le propriétaire
- **Date :** 2026-11-15
- **Références :** `docs/PLAN.md` M4.02q à M4.02y ; `docs/ui-design.md`, section « Langues de la console » ; `docs/ARCHITECTURE.md` §10bis.1 ; `docs/CONVENTIONS.md` §11.5

## Contexte

- Le propriétaire a demandé le 2026-11-15 que la console et sa documentation existent au moins en anglais, en français, en italien et en espagnol : une console en français qui affiche une documentation en anglais n'est pas acceptable.
- Le Web n'accepte que `en` et `fr` (`UseRequestLocalization`, culture par défaut `en`), choisies par l'en-tête `Accept-Language` seulement : rien n'écrit le cookie de culture. `Common.resx` et `UiStrings.resx` n'ont qu'une traduction `.fr.resx`.
- La documentation publiée (M4.02o) est servie en anglais ; `DocumentationLibrary` sait servir `{slug}.fr.md`, mais aucune traduction n'existe, et les ancres viennent du texte des titres.
- Beaucoup de textes du produit ne sont pas lus dans la console : réponses de l'Api, journaux, messages de l'uploader, commentaires et statuts publiés sur une forge. Sans décision écrite, chaque micro-feature trancherait seule ce qu'elle traduit.

## Décision

### Langues

- **Anglais** (`en`) : langue de référence, par défaut et de repli. **Français** (`fr`), **italien** (`it`), **espagnol** (`es`) : traductions.
- Les cultures sont **neutres** : une variante régionale du navigateur (`it-CH`, `es-MX`, `fr-CA`) retombe sur sa langue.
- Une langue de plus est une ligne de `ConsoleLanguages` (M4.02s), ses deux fichiers `Common.{langue}.resx` et `UiStrings.{langue}.resx` et, au besoin, ses traductions de documents (M4.02v). Un test vérifie que chaque langue a exactement les clés de l'anglais.

### Choix de la langue

1. Le **cookie de culture** d'ASP.NET Core (`.AspNetCore.Culture`), posé par le sélecteur de la console (M4.02t) et, pour un utilisateur connecté, par la langue retenue par son compte (M4.02u) ;
2. sans cookie, l'en-tête **`Accept-Language`** ;
3. sinon l'**anglais**.

Le paramètre de requête `?culture=` n'est **pas** accepté (pas de `QueryStringRequestCultureProvider`) : une adresse partagée ne change jamais la langue de celui qui l'ouvre.

### Ce qui reste en anglais

La liste est fermée ; chaque élément reste en anglais sans réglage.

| Élément | Raison |
|---|---|
| L'Api : réponses `ProblemDetails`, codes d'erreur, OpenAPI, Swagger et Scalar | Elle est lue par des programmes et des pipelines, pas par la console ; un client traduit un code d'erreur stable, jamais un texte. |
| L'uploader `coverage-upload` (messages, `--help`, journaux), l'action composite et le template Azure Pipelines | Ils tournent sur des agents CI dont la langue n'est pas celle de l'utilisateur, et leurs journaux sont lus et cherchés par toute une équipe. |
| Les journaux Serilog des deux hôtes | Ils servent au diagnostic, à la recherche et aux alertes : un message doit avoir le même texte partout. |
| Ce qui est publié sur une forge : commentaires de pull request, statuts de commit et leurs noms, badges | Ils doivent rester ceux de Codecov (règle critique 1), et un dépôt est lu par des personnes de toutes langues. |
| Les identifiants stockés : codes d'erreur, codes du journal de traitement, actions d'audit, clés de réglages | Ce sont des données ; ils sont traduits seulement à l'affichage. |
| La ligne de commande d'administration (M1.22a) | Elle est un outil d'exploitation, au même titre que les journaux. |
| Les documents internes jamais publiés : plan, ADR, architecture, conventions | Ils gardent leur langue actuelle ; ils ne sont pas servis par la console. |

### Documentation

- Un document publié est écrit en **anglais**, puis traduit à côté de lui (`{nom}.{langue}.md`), selon les règles de `docs/CONVENTIONS.md` (M4.02v).
- Une page sans traduction dans la langue de la console s'affiche en anglais avec un avis ; ses ancres sont celles de la page anglaise dans toutes les langues.

## Conséquences

- **Positives :**
  - une frontière nette : seul le Web contient des ressources et référence `Microsoft.Extensions.Localization`, ce que `Architecture.Tests` garde (`LocalizationBoundaryTests`) ;
  - une langue de plus ne touche aucune couche hors du Web ;
  - les textes lus par des programmes, des équipes ou des forges restent stables et compatibles avec Codecov.
- **Négatives :**
  - chaque nouvelle clé de ressource est écrite dans quatre langues, et chaque modification d'un document publié met à jour ses traductions ;
  - l'italien et l'espagnol, écrits sans locuteur natif dans l'équipe, demandent une relecture ;
  - un utilisateur non anglophone lit en anglais les messages de l'uploader et les commentaires de pull request.
