---
source-sha256: d260ebdd09fde1852bfa8ff322f28206b7182844a700ca34930b4877df46fd4c
---
# Despliegue en Azure Container Apps

Un alojamiento opcional (M4.03; el destino de producción es la VM de [operations.md](operations.md)).
[`deploy/azure/main.bicep`](../deploy/azure/main.bicep) lo describe; el repositorio no ejecuta ningún despliegue.

## Qué crea

| Recurso | Configuración |
|---|---|
| Identidad administrada asignada por el usuario | Leída por ambos hosts mediante `AZURE_CLIENT_ID` (`DefaultAzureCredential`). |
| Key Vault (RBAC, eliminación temporal, protección de purga) | La clave RSA `dataprotection`, que protege el anillo de claves de Data Protection almacenado en la base de datos (`DataProtection__KeyVaultKeyId`), y el secreto `connection-string`. La identidad tiene *Key Vault Crypto Service Encryption User* y *Key Vault Secrets User*. |
| PostgreSQL Flexible Server 17 (`databaseKind=PostgreSql`, predeterminado) | Burstable B1ms, 32 GB, restauración a un momento dado de 14 días (ARCHITECTURE §12.5). |
| o Azure SQL (`databaseKind=SqlServer`) | S0; READ_COMMITTED_SNAPSHOT está activado de forma predeterminada, y `/health/ready` lo comprueba. |
| Log Analytics y un entorno de Container Apps | Los registros de los contenedores. |
| Container App `-api` | De 1 a `apiMaxReplicas` réplicas, **nunca cero**: ejecuta el worker de jobs y las migraciones (`MigrateOnStartup`). |
| Container App `-web` | Una réplica, sesiones persistentes (circuitos de Blazor Server). |

Ambas apps reciben solo los valores de arranque (cadena de conexión por referencia de Key Vault, proveedor, clave de Data
Protection, identidad, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` detrás del ingress), y los
sondeos: startup y liveness en `/health/live`, readiness en `/health/ready` (base de datos accesible,
migraciones aplicadas, RCSI activado con SQL Server). Todo ajuste funcional permanece en la base de datos, editado
en la consola (regla 7 de AGENT.md).

## Desplegar

```bash
az group create -n coverage-rg -l westeurope
az deployment group create -g coverage-rg -f deploy/azure/main.bicep \
  -p namePrefix=cc apiImage=ghcr.io/<owner>/code-coverage-api:1.2.3 \
     webImage=ghcr.io/<owner>/code-coverage-web:1.2.3 databaseKind=PostgreSql \
     databaseAdminPassword='<a strong password>'
```

Las salidas `apiUrl` y `webUrl` son las dos direcciones públicas: asócieles sus propios nombres de host
(dominios personalizados con certificados administrados) antes de crear la GitHub App, y luego abra `/setup` en
la Web. Las imágenes deben poder leerse desde el entorno (paquetes GHCR públicos, o credenciales del registro
añadidas a las apps).

**Actualización:** vuelva a desplegar con los nuevos tags de imagen; la revisión de la Api migra el esquema, la Web la espera
(no está lista hasta que el esquema esté al día). La Api se actualiza primero gracias a la dependencia de la
plantilla.

## Comprobar un despliegue (paso del propietario)

- `az containerapp show -n cc-api -g coverage-rg --query properties.template.scale.minReplicas` vale 1.
- `curl https://<api>/health/ready` responde 200; en Azure SQL, la comprobación de readiness confirma RCSI.
- El inicio de sesión con GitHub de la Web redirige a un callback `https://` (cabeceras reenviadas respetadas).

## No verificado en el repositorio

`az bicep build` y su linter no se ejecutan: la CLI de Bicep no forma parte de la cadena de herramientas del
repositorio. La plantilla solo se ha revisado; un despliegue de prueba la valida.

## Aislamiento y coste

Una copia de seguridad de la base de datos y el área de trabajo de Log Analytics contienen todas las organizaciones de la instancia (ADR-013):
restrinja quién puede leerlas. Los niveles más pequeños indicados cuestan unas decenas de euros al mes; escale la
base de datos y la Api según la carga medida del piloto.
