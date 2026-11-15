---
source-sha256: d260ebdd09fde1852bfa8ff322f28206b7182844a700ca34930b4877df46fd4c
---
# Deployment su Azure Container Apps

Un hosting opzionale (M4.03; la destinazione di produzione è la VM di [operations.md](operations.md)).
[`deploy/azure/main.bicep`](../deploy/azure/main.bicep) lo descrive; il repository non esegue alcun deployment.

## Cosa crea

| Risorsa | Impostazioni |
|---|---|
| Identità gestita assegnata dall’utente | Letta da entrambi gli host tramite `AZURE_CLIENT_ID` (`DefaultAzureCredential`). |
| Key Vault (RBAC, soft delete, protezione dall’eliminazione definitiva) | La chiave RSA `dataprotection`, che protegge il key ring di Data Protection memorizzato nel database (`DataProtection__KeyVaultKeyId`), e il secret `connection-string`. L’identità ha *Key Vault Crypto Service Encryption User* e *Key Vault Secrets User*. |
| PostgreSQL Flexible Server 17 (`databaseKind=PostgreSql`, predefinito) | Burstable B1ms, 32 GB, ripristino temporizzato 14 giorni (ARCHITECTURE §12.5). |
| oppure Azure SQL (`databaseKind=SqlServer`) | S0; READ_COMMITTED_SNAPSHOT è attivo per impostazione predefinita, e `/health/ready` lo verifica. |
| Log Analytics e un ambiente Container Apps | I log dei container. |
| Container App `-api` | Da 1 a `apiMaxReplicas` repliche, **mai zero**: esegue il job worker e le migrazioni (`MigrateOnStartup`). |
| Container App `-web` | Una replica, sessioni persistenti (circuiti Blazor Server). |

Entrambe le app ricevono solo i valori di bootstrap (stringa di connessione tramite riferimento Key Vault, provider, chiave
di Data Protection, identità, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` dietro l’ingress) e le
probe: startup e liveness su `/health/live`, readiness su `/health/ready` (database raggiungibile,
migrazioni applicate, RCSI attivo sotto SQL Server). Ogni impostazione funzionale resta nel database, modificata
nella console (AGENT.md regola 7).

## Deployment

```bash
az group create -n coverage-rg -l westeurope
az deployment group create -g coverage-rg -f deploy/azure/main.bicep \
  -p namePrefix=cc apiImage=ghcr.io/<owner>/code-coverage-api:1.2.3 \
     webImage=ghcr.io/<owner>/code-coverage-web:1.2.3 databaseKind=PostgreSql \
     databaseAdminPassword='<a strong password>'
```

Gli output `apiUrl` e `webUrl` sono i due indirizzi pubblici: associarvi i propri nomi host
(domini personalizzati con certificati gestiti) prima di creare la GitHub App, poi aprire `/setup` sul
Web. Le immagini devono essere leggibili dall’ambiente (pacchetti GHCR pubblici, o credenziali del registry
aggiunte alle app).

**Aggiornamento:** eseguire di nuovo il deployment con i nuovi tag delle immagini; la revisione dell’Api migra lo schema, il Web la attende
(non pronto finché lo schema non è aggiornato). L’Api viene aggiornata per prima grazie alla dipendenza del
template.

## Verificare un deployment (passo dell’owner)

- `az containerapp show -n cc-api -g coverage-rg --query properties.template.scale.minReplicas` vale 1.
- `curl https://<api>/health/ready` risponde 200; su Azure SQL, il controllo di readiness conferma RCSI.
- L’accesso GitHub del Web reindirizza a una callback `https://` (forwarded headers rispettati).

## Non verificato nel repository

`az bicep build` e il suo linter non vengono eseguiti: la Bicep CLI non fa parte della toolchain del
repository. Il template è stato solo rivisto; un deployment di test lo convalida.

## Isolamento e costi

Un backup del database e il workspace Log Analytics contengono tutte le organizzazioni dell’istanza (ADR-013):
limitare chi può leggerli. I tier più piccoli indicati sopra costano qualche decina di euro al mese; dimensionare il
database e l’Api per il carico misurato del pilota.
