---
source-sha256: 9a6e1ebbd225f68090327b2f36be71d3431ba7cc31e7cb946bcadcf3c9b94246
---
# Comportamenti di GitHub da verificare su due account reali (M4.02a)

ADR-013 si basa su comportamenti di GitHub che non è stato possibile eseguire dal repository («Probable» nell’ADR).
Questo elenco viene eseguito dall’owner su due account reali prima del passaggio (M4.08): l’account **A** (un’organizzazione
GitHub) e l’account **B** (una seconda organizzazione, o un account personale dove una riga lo
indica), entrambi con l’App di un’istanza di test installata. Ogni riga viene registrata come **confermata**,
**corretta** (con la modifica al piano che apre) o **non verificabile** (con il suo rischio), e datata. Una
riga confermata passa da «Probable» a «Proven» in ADR-013.

| # | Comportamento | Procedura | Atteso (dal codice) | Prove da conservare | Risultato | Data |
|---|---|---|---|---|---|---|
| 1 | SAML e `GET /user/installations` | Su un’organizzazione con single sign-on SAML (GitHub Enterprise Cloud), accedere al Web con un membro che **non** ha autorizzato l’SSO per la OAuth App, poi con uno che l’ha fatto. | Senza autorizzazione SSO, `user/installations` omette l’organizzazione: l’utente non ne vede alcun repository (`GitHubUserGateway.ListInstallationsAsync`). Con l’autorizzazione, l’organizzazione è elencata. | I due elenchi di `/` dopo l’accesso; la risposta dell’API (`gh api user/installations` con il token dell’utente). | | |
| 2 | Collaboratori esterni in quell’elenco | Aggiungere un collaboratore esterno a un repository di A; accedere con quell’utente. | `user/installations` elenca l’installazione di A con i soli repository che il collaboratore può leggere (`user/installations/{id}/repositories`); il ruolo nell’organizzazione è nessuno, l’accesso al repository in lettura o scrittura. | L’elenco dei repository mostrato; il ruolo risolto su `/orgs/github/A`. | | |
| 3 | Contenuto dell’evento `transferred` | Trasferire un repository da A a B. | Un evento `repository` `transferred` raggiunge l’istanza con l’id del repository invariato e il nuovo owner; `ReconcileRepositoryTransfer` chiude la riga in A e `SyncInstallations` (chiave di trasferimento) la crea in B (`HandleWebhookDeliveryCommand`). | La consegna del webhook (Recent deliveries dell’App) e le righe su `/admin/operations`. | | |
| 4 | `installation_repositories` removed, ricevuto dall’origine | Durante il trasferimento della riga 3. | L’installazione di A riceve `installation_repositories` con il repository in `repositories_removed` (quando A aveva selezionato i repository). | La consegna. | | |
| 5 | `organization` renamed | Rinominare l’organizzazione A. | Arriva un evento `organization` `renamed`; `RewriteRepositoryOwner` riscrive l’owner dei repository di A; il vecchio login non viene più risolto. | La consegna; il nuovo URL funziona, il vecchio risponde not found. | | |
| 6 | `installation_target` renamed, su entrambi i tipi di account | Rinominare A (organizzazione) e B (account personale, se disponibile). | Arriva un evento `installation_target` `renamed` per ciascuno; la riga dell’organizzazione assume il nuovo login (`GitHubInstallationTargetEvent`). | Le consegne; `/admin/organizations`. | | |
| 7 | Secret di Actions dopo un trasferimento | Prima della riga 3, aggiungere un secret di repository `CODECOV_TOKEN` al repository trasferito. | Registrare se GitHub conserva il secret dopo il trasferimento. Qualunque cosa faccia, un token di repository importato di A non funziona più in B (rifiutato), e per i token dell’organizzazione di A viene consigliata la rotazione (`RotationAdvisedAt`). | Il rifiuto dell’upload in B; il banner in A. | | |
| 8 | Un repository pubblico di un altro account letto da un token di installazione | Con il token di installazione di B, leggere un repository pubblico di A su cui B non ha installato l’App. | GitHub risponde (dati pubblici); l’istanza non lo usa mai, poiché ogni lettura passa dall’installazione dell’organizzazione a cui appartiene il repository. | La risposta dell’API. | | |
| 9 | La forma `/repositories/{id}` su ogni endpoint | Eseguire una notifica di una pull request in A, poi rinominare il repository ed eseguirne un’altra. | Ogni chiamata qui sotto funziona tramite l’id del repository prima e dopo la ridenominazione (inventario qui sotto). | Le chiamate nei log (`github_requests_total` per endpoint) e gli stati dopo la ridenominazione. | | |

Quando la riga 1 non può essere eseguita (nessuna organizzazione GitHub Enterprise Cloud a disposizione), registrarla come **non verificabile**
con il suo rischio: un membro senza autorizzazione SSO vede meno repository di quanti ne possa leggere su GitHub;
nulla trapela.

## Inventario dei percorsi del gateway

Le chiamate di `GitHubAppGateway` (token di installazione salvo diversa indicazione). Un repository tracciato è
sempre indirizzato tramite il suo id, in modo che una ridenominazione non rompa nulla; solo la ricerca di un repository per nome
usa owner e nome.

| Chiamata | Percorso |
|---|---|
| Conversione del manifest (JWT dell’App) | `POST app-manifests/{code}/conversions` |
| Installazioni dell’App (JWT dell’App) | `GET app/installations`, `GET app/installations/{id}`, `POST app/installations/{id}/access_tokens` (`GitHubAppTokenProvider`) |
| Repository di un’installazione | `GET installation/repositories` |
| Ricerca di un repository per nome | `GET repos/{owner}/{name}/installation` (JWT dell’App), poi `GET repos/{owner}/{name}` |
| Appartenenza di un utente | `GET orgs/{org}/memberships/{user}` |
| Branch | `GET repositories/{id}/branches/{branch}` |
| Commit | `GET repositories/{id}/commits?sha=…`, `GET repositories/{id}/commits/{sha}` |
| Confronto | `GET repositories/{id}/compare/{base}...{head}` |
| Pull request e i suoi file | `GET repositories/{id}/pulls/{n}`, `GET repositories/{id}/pulls/{n}/files` |
| Alberi e contenuti | `GET repositories/{id}/git/trees/{sha}`, `GET repositories/{id}/contents/{path}?ref=…` |
| Stati | `POST repositories/{id}/statuses/{sha}` |
| Check run (M5.05) | `POST repositories/{id}/check-runs`, `PATCH repositories/{id}/check-runs/{id}` |
| Commenti | `GET\|POST repositories/{id}/issues/{n}/comments`, `PATCH\|DELETE repositories/{id}/issues/comments/{id}` |

`GitHubUserGateway` (token dell’utente): `GET user`, `GET user/installations`,
`GET user/installations/{id}/repositories`.
