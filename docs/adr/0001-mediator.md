# ADR-001 : Médiateur martinothamar `Mediator` 3.x

- **Statut :** accepté (spike M0.07 concluant, 2026-11-07)
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §3.3, §3.4 et §13.3 ; `docs/CONVENTIONS.md` §5.5, §5.6 et §16.3 point 2 ; `docs/PLAN.md` M0.07, M0.08 et §11-1

## Contexte

- Le médiateur le plus répandu en .NET est `MediatR`, dont la dernière version gratuite est la 12.x (les versions 13 et suivantes sont sous licence commerciale).
- L'utilisateur exige de remplacer MediatR. Ce choix doit être acté par une décision d'architecture (CONVENTIONS §5.5).
- Les behaviors du pipeline s'appuient sur un petit nombre de briques : requête, handler, behavior de pipeline, délégué `next`, `ISender` et notification. Toute alternative doit les fournir.
- Trois options ont été comparées :
  - A. un dispatcher maison d'environ cinq types dans `Application/Messaging` ;
  - B. `Mediator` (martinothamar, licence MIT, générateur de source) ;
  - C. rester sur `MediatR` 12.5, contraire à l'exigence.
- L'Api, le Web et le worker exécutent les mêmes commandes et requêtes de l'Application (ADR-002) : le médiateur est le point d'entrée unique de tous les cas d'usage.

## Décision

- Le médiateur est **martinothamar `Mediator` 3.x** (`Mediator.Abstractions` et `Mediator.SourceGenerator`, version majeure flottante `3.*`).
- **Le générateur de source est référencé uniquement par Application** (`PrivateAssets="all"`). C'est le seul projet qui le référence dans chaque artefact déployé (Api et Web) ; le code généré voit les handlers `internal sealed` sans `InternalsVisibleTo`.
- Les messages sont des `ICommand<T>` et des `IQuery<T>` ; les handlers implémentent `ICommandHandler<,>` et `IQueryHandler<,>`.
- **Écart assumé :** la méthode s'appelle `Handle` et renvoie `ValueTask<T>`, au lieu de `HandleAsync` et `Task<T>`, parce que le nom et le type sont imposés par la bibliothèque. Les actions de contrôleur gardent le suffixe `Async` (CONVENTIONS §5.5 et §16.2).
- Le pipeline est, dans cet ordre : **Logging → Authorization → Validation**. Il est déclaré dans `AddMediator` (`PipelineBehaviors`), avec `ServiceLifetime.Scoped` (le `DbContext` est scoped), `GenerateTypesAsInternal = true` et l'espace de noms `CodeCoverage.Application.Messaging`.
- Les appelants sont `ISender` (Api, et worker avec un scope par job) et `ScopedSender` enveloppé par `UiRequestRunner` (Web, un scope DI par requête).
- Le MVP n'a pas d'événements de domaine : les enchaînements passent par la file de jobs (ADR-005). `IDomainEvent` et `IHasDomainEvents` existent dans le Domain pour l'avenir, à la place d'`INotification`.

## Conséquences

- **Positives :**
  - aucune dépendance sous licence commerciale ; pas de réflexion à l'exécution, la résolution des handlers est générée à la compilation ;
  - une API proche de MediatR : les behaviors écrits pour MediatR se transposent avec peu d'adaptations.
- **Négatives :**
  - écart de nommage avec la convention du projet (`Handle`/`ValueTask` au lieu du suffixe `Async`), documenté ici et pris en compte par les tests d'architecture ;
  - la signature de `IPipelineBehavior` change entre versions majeures (ordre des paramètres en v3 : message, `next`, `cancellationToken`). Un test de compilation dans Application.Tests la fige ;
  - le README v3 recommande d'installer le générateur dans le projet exécutable le plus externe. Le montage dans une bibliothèque est un risque identifié (ARCHITECTURE §13.3).
- **Validation et repli :**
  - le spike M0.07 valide le montage (smoke test `Ping` envoyé depuis Application.Tests, Api et Web). En cas de succès, ce document passe à « accepté » ;
  - **résultat du spike (M0.07, `Mediator` 3.0.2) :** montage concluant, le repli n'est pas appliqué. Le générateur, référencé par Application seule, produit le médiateur interne dans `CodeCoverage.Application.Messaging` ; `PingQuery` est résolu et traité depuis l'Api et le Web (`MediatorWiringTests`) sans `InternalsVisibleTo` vers les hôtes, et `MediatorSignatureTests` fige la signature v3 de `IPipelineBehavior` ;
  - en cas d'échec, le repli est appliqué et ce document est mis à jour : générateur référencé par Api et Web avec `InternalsVisibleTo`, et `Assemblies = [Application]` (PLAN §11-1).
