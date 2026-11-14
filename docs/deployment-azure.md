# Deployment on Azure Container Apps

An optional hosting (M4.03; the production target is the VM of [operations.md](operations.md)).
[`deploy/azure/main.bicep`](../deploy/azure/main.bicep) describes it; the repository runs no deployment.

## What it creates

| Resource | Settings |
|---|---|
| User-assigned managed identity | Read by both hosts through `AZURE_CLIENT_ID` (`DefaultAzureCredential`). |
| Key Vault (RBAC, soft delete, purge protection) | The RSA key `dataprotection`, which protects the Data Protection key ring stored in the database (`DataProtection__KeyVaultKeyId`), and the secret `connection-string`. The identity has *Key Vault Crypto Service Encryption User* and *Key Vault Secrets User*. |
| PostgreSQL Flexible Server 17 (`databaseKind=PostgreSql`, default) | Burstable B1ms, 32 GB, point-in-time restore 14 days (ARCHITECTURE §12.5). |
| or Azure SQL (`databaseKind=SqlServer`) | S0; READ_COMMITTED_SNAPSHOT is on by default, and `/health/ready` checks it. |
| Log Analytics and a Container Apps environment | The logs of the containers. |
| Container App `-api` | 1 to `apiMaxReplicas` replicas, **never zero**: it runs the job worker and the migrations (`MigrateOnStartup`). |
| Container App `-web` | One replica, sticky sessions (Blazor Server circuits). |

Both apps get the bootstrap values only (connection string by Key Vault reference, provider, Data
Protection key, identity, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` behind the ingress), and the
probes: startup and liveness on `/health/live`, readiness on `/health/ready` (database reachable,
migrations applied, RCSI on under SQL Server). Every functional setting stays in the database, edited
in the console (AGENT.md rule 7).

## Deploying

```bash
az group create -n coverage-rg -l westeurope
az deployment group create -g coverage-rg -f deploy/azure/main.bicep \
  -p namePrefix=cc apiImage=ghcr.io/<owner>/code-coverage-api:1.2.3 \
     webImage=ghcr.io/<owner>/code-coverage-web:1.2.3 databaseKind=PostgreSql \
     databaseAdminPassword='<a strong password>'
```

The outputs `apiUrl` and `webUrl` are the two public addresses: bind your own host names to them
(custom domains with managed certificates) before you create the GitHub App, then open `/setup` on
the Web. The images must be readable by the environment (public GHCR packages, or registry
credentials added to the apps).

**Update:** deploy again with the new image tags; the Api revision migrates the schema, the Web waits
for it (not ready until the schema is current). The Api is updated first by the dependency of the
template.

## Checking a deployment (owner's step)

- `az containerapp show -n cc-api -g coverage-rg --query properties.template.scale.minReplicas` is 1.
- `curl https://<api>/health/ready` answers 200; on Azure SQL, the readiness check confirms RCSI.
- The GitHub sign-in of the Web redirects to an `https://` callback (forwarded headers honoured).

## Not verified in the repository

`az bicep build` and its linter are not run: the Bicep CLI is not part of the toolchain of the
repository. The template was reviewed only; a test deployment validates it.

## Isolation and cost

A database backup and the Log Analytics workspace hold every organization of the instance (ADR-013):
restrict who can read them. The smallest tiers above cost a few tens of euros a month; scale the
database and the Api for the measured load of the pilot.
