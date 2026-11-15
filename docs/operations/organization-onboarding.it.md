---
source-sha256: 73f1dd082c287d4922d9bf73ab66f3b052f44fb78c286fced2d9be7b50470848
---
# Onboarding di un’organizzazione

Come un account GitHub (un’organizzazione o un utente) entra in un’istanza CodeCoverage condivisa da più
organizzazioni isolate (ADR-013). L’amministratore dell’istanza la ammette; un amministratore dell’organizzazione
poi la configura. Nulla di un’organizzazione è visibile da un’altra.

## 1. Installare l’App

Un owner dell’account GitHub installa la GitHub App dell’istanza da
`https://github.com/apps/<slug>/installations/new`, su tutti i suoi repository o su una selezione.
L’amministratore dell’istanza trova quel link nell’intestazione di `/admin/organizations` (e alla fine di
`/setup`) e lo invia all’owner. L’App richiede:

| Permesso | Perché |
|---|---|
| Contents: read, Metadata: read | Il diff, gli alberi, il `codecov.yml`, il sorgente annotato. |
| Commit statuses: read & write | Gli stati `coverage/*` (poi `codecov/*`). |
| Pull requests: read & write | I dati della pull request e il commento. |
| Checks: read & write | Le Check Run, una volta che l’organizzazione attiva `Notifications:ChecksEnabled` (M5.05). Un’App creata prima di M5.05 lo ottiene quando i maintainer lo aggiungono; ogni installazione lo accetta poi da GitHub. |
| Organization members: read | I ruoli dell’organizzazione (admin, membro) nella console. |

L’installazione crea un’organizzazione **in attesa**: non traccia ancora nulla e ogni upload viene rifiutato
(401 `Upload.TokenInvalid` con un token, `Repository.NotFound` con OIDC; l’uploader esce comunque con 0).
GitHub rimanda poi l’owner alla console. Se l’organizzazione in attesa non compare (il suo
webhook è andato perso), l’amministratore dell’istanza clicca **Verifica le installazioni** in `/admin/organizations`:
il worker elenca subito le installazioni dell’App, invece di attendere l’esecuzione notturna.

## 2. Ammetterla

L’amministratore dell’istanza ammette l’organizzazione in attesa in `/admin/organizations` (**Ammetti** sulla sua
riga), oppure ammette un account prima della sua installazione con **Ammetti un account**: si digita il login, la
console legge l’id dell’account da GitHub (`GET /users/{login}`) e mostra «login → id» per
conferma. Un login sconosciuto viene rifiutato (`Organization.UnknownGitHubLogin`) e nulla viene salvato.
L’amministratore che ammette vede subito l’organizzazione, senza dover accedere di nuovo. Procedura di emergenza,
senza la console (docs/operations.md, «La riga di comando di amministrazione»):

```bash
dotnet CodeCoverage.Api.dll admin admit-organization --login my-org --account-id 123456
```

L’id dell’account è l’id numerico dell’account GitHub (`gh api users/my-org --jq .id`). L’ammissione
avvia la sincronizzazione dei repository dell’installazione.

## 3. Configurarla

Un amministratore dell’organizzazione (un owner dell’organizzazione GitHub, o l’utente di un account
personale) apre `/orgs/github/<org>/settings`. Le cinque impostazioni di un’organizzazione:

| Impostazione | Predefinito | Quando cambiarla |
|---|---|---|
| `Notifications:StatusContextPrefix` | `coverage` | `codecov` al passaggio (docs/cutover-runbook.md). |
| `Notifications:StatusesEnabled` | `true` | Disattivata per mantenere CodeCoverage silenzioso su GitHub. |
| `Notifications:CommentsEnabled` | `true` | Disattivata per lasciare soli i commenti di Codecov sulle pull request durante il pilota. |
| `Notifications:ChecksEnabled` | `false` | Attivata al passaggio, una volta che l’installazione ha accettato il permesso Checks. |
| `Coverage:OrganizationYaml` | nessuno | Uno YAML condiviso da tutti i repository (sotto il `codecov.yml` proprio di ciascun repository). |

## 4. Token e badge

- **OIDC** non richiede nulla: un job GitHub Actions con `id-token: write` dimostra il proprio repository.
- **Token di upload:** importare i valori `CODECOV_TOKEN` (organizzazione e repository) in
  `/orgs/github/<org>/upload-tokens`, tramite l’API, oppure con `tools/rollout/Import-CodecovTokens.ps1`.
  Un valore è univoco solo all’interno dell’organizzazione.
- I **token API** (`cvp_…`, per gli script) sono creati da ciascun utente nella console; agiscono con i
  diritti del loro utente, in una sola organizzazione.
- **Token dei badge** dei repository privati: importare il graphing token di Codecov nelle impostazioni di ciascun
  repository, oppure generarne uno nuovo.

## 5. Cosa un’organizzazione non può configurare (ADR-013)

Questi punti derivano dall’isolamento delle organizzazioni e sono uguali per tutti:

- l’App deve essere installata e l’organizzazione ammessa prima che qualsiasi cosa venga accettata;
- nessun token di upload globale: un token funziona solo nell’organizzazione che lo ha creato o importato;
- un repository trasferito riparte da zero nella sua nuova organizzazione (storico vuoto, nessun token di upload, nuovo
  token del badge); il suo storico resta alla vecchia organizzazione;
- dopo la ridenominazione dell’account GitHub, il vecchio login non viene più risolto e nulla reindirizza;
- l’amministratore dell’istanza vede solo metadati (`/admin/operations`, `/admin/organizations`), mai
  la copertura di un’organizzazione; gli operatori del database, dei backup e del monitoraggio vedono
  tutto (docs/operations.md).

I login riservati del primo progetto (un login uguale a un segmento di route del prodotto) non esistono più:
l’owner non è più il primo segmento di una route (ADR-014).

## 6. Uscire

Disinstallare l’App sospende l’organizzazione: i suoi dati sono conservati (nessuna cancellazione per ora), i suoi upload sono
rifiutati, e reinstallarla dallo stesso account la ricollega.
