# ADR-006 : Paramètres propagés par interrogation de `SettingsRevision`

- **Statut :** accepté
- **Date :** 2026-11-07
- **Références :** `docs/ARCHITECTURE.md` §4bis ; `docs/CONVENTIONS.md` §12 ; `docs/PLAN.md` M0.18 à M0.21

## Contexte

- L'utilisateur exige des paramètres **modifiables à chaud depuis l'UI**, plutôt que dans `appsettings.json`.
- Les paramètres sont stockés en base et exposés dans `IConfiguration` par un provider dédié (CONVENTIONS §12.2). Les consommateurs lisent `IOptionsMonitor<T>`.
- Deux hôtes lisent les mêmes paramètres (ADR-002) : une modification enregistrée par le Web doit atteindre l'Api, et inversement.
- PostgreSQL offre `LISTEN/NOTIFY`, SQL Server n'a pas d'équivalent simple ; le mécanisme doit être portable.

## Décision

- Une table à une ligne, **`SettingsRevision`**, porte un compteur.
- `UpdateSetting` et `ReplaceSecret` écrivent la valeur **et** incrémentent `Revision` dans la même transaction.
- Dans chaque hôte, `SettingsChangeWatcher` (`BackgroundService`, `PeriodicTimer` de `Settings:PollSeconds`, 5 s par défaut) lit la révision. Si elle a changé, il recharge le provider de configuration, ce qui notifie les consommateurs `IOptionsMonitor`.
- L'hôte qui enregistre se rafraîchit immédiatement ; l'autre suit en 5 secondes au plus.
- **Pas de `LISTEN/NOTIFY`**, ni de bus de messages.

## Conséquences

- **Positives :**
  - mécanisme identique sur PostgreSQL et SQL Server, testable sans infrastructure supplémentaire ;
  - coût négligeable : une lecture d'une ligne toutes les 5 secondes par hôte.
- **Négatives :**
  - délai de propagation d'au plus `Settings:PollSeconds` vers l'autre hôte ;
  - les paramètres marqués `RequiresRestart` ne sont pas concernés et demandent un redémarrage, signalé dans l'UI.
