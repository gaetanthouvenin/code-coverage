---
source-sha256: d260ebdd09fde1852bfa8ff322f28206b7182844a700ca34930b4877df46fd4c
---
# Déploiement sur Azure Container Apps

Un hébergement facultatif (M4.03 ; la cible de production est la VM de [operations.md](operations.md)).
[`deploy/azure/main.bicep`](../deploy/azure/main.bicep) le décrit ; le dépôt n’exécute aucun déploiement.

## Ce qu’il crée

| Ressource | Réglages |
|---|---|
| Identité managée affectée par l’utilisateur | Lue par les deux hôtes via `AZURE_CLIENT_ID` (`DefaultAzureCredential`). |
| Key Vault (RBAC, suppression réversible, protection contre la purge) | La clé RSA `dataprotection`, qui protège le trousseau de clés Data Protection stocké en base de données (`DataProtection__KeyVaultKeyId`), et le secret `connection-string`. L’identité a les rôles *Key Vault Crypto Service Encryption User* et *Key Vault Secrets User*. |
| PostgreSQL Flexible Server 17 (`databaseKind=PostgreSql`, par défaut) | Burstable B1ms, 32 Go, restauration à un instant donné sur 14 jours (ARCHITECTURE §12.5). |
| ou Azure SQL (`databaseKind=SqlServer`) | S0 ; READ_COMMITTED_SNAPSHOT est activé par défaut, et `/health/ready` le vérifie. |
| Log Analytics et un environnement Container Apps | Les journaux des conteneurs. |
| Container App `-api` | De 1 à `apiMaxReplicas` réplicas, **jamais zéro** : elle fait tourner le worker des jobs et les migrations (`MigrateOnStartup`). |
| Container App `-web` | Un seul réplica, sessions persistantes (circuits Blazor Server). |

Les deux applications ne reçoivent que les valeurs d’amorçage (chaîne de connexion par référence Key Vault, fournisseur, clé
Data Protection, identité, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` derrière l’ingress), et les
sondes : startup et liveness sur `/health/live`, readiness sur `/health/ready` (base de données joignable,
migrations appliquées, RCSI activé sous SQL Server). Tout paramètre fonctionnel reste en base de données, modifié
dans la console (règle 7 d’AGENT.md).

## Déployer

```bash
az group create -n coverage-rg -l westeurope
az deployment group create -g coverage-rg -f deploy/azure/main.bicep \
  -p namePrefix=cc apiImage=ghcr.io/<owner>/code-coverage-api:1.2.3 \
     webImage=ghcr.io/<owner>/code-coverage-web:1.2.3 databaseKind=PostgreSql \
     databaseAdminPassword='<a strong password>'
```

Les sorties `apiUrl` et `webUrl` sont les deux adresses publiques : liez-y vos propres noms d’hôte
(domaines personnalisés avec certificats managés) avant de créer la GitHub App, puis ouvrez `/setup` sur
le Web. Les images doivent être lisibles par l’environnement (packages GHCR publics, ou identifiants de registre
ajoutés aux applications).

**Mise à jour :** redéployez avec les nouveaux tags d’image ; la révision de l’Api migre le schéma, le Web l’attend
(non prêt tant que le schéma n’est pas à jour). L’Api est mise à jour en premier grâce à la dépendance du
template.

## Vérifier un déploiement (étape du propriétaire)

- `az containerapp show -n cc-api -g coverage-rg --query properties.template.scale.minReplicas` vaut 1.
- `curl https://<api>/health/ready` répond 200 ; sur Azure SQL, la vérification de readiness confirme RCSI.
- La connexion GitHub du Web redirige vers un callback `https://` (en-têtes transférés pris en compte).

## Non vérifié dans le dépôt

`az bicep build` et son linter ne sont pas exécutés : la CLI Bicep ne fait pas partie de la chaîne d’outils du
dépôt. Le template a seulement été relu ; un déploiement de test le valide.

## Isolation et coût

Une sauvegarde de la base de données et l’espace de travail Log Analytics contiennent toutes les organisations de l’instance (ADR-013) :
restreignez qui peut les lire. Les niveaux les plus petits ci-dessus coûtent quelques dizaines d’euros par mois ; dimensionnez la
base de données et l’Api selon la charge mesurée du pilote.
