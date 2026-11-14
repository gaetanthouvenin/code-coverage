// ------------------------------------------------------------------------------------------------
// CodeCoverage - Azure Container Apps (M4.03, optional hosting; docs/deployment-azure.md).
// Two Container Apps (the Api, never scaled to zero, and the Web, one replica), PostgreSQL Flexible
// Server or Azure SQL, a user-assigned managed identity, and a Key Vault that holds the Data
// Protection key and the connection string. No deployment is run by the repository: a test
// deployment is the owner's step.
//
//   az deployment group create -g <resource group> -f deploy/azure/main.bicep \
//     -p namePrefix=cc apiImage=ghcr.io/<owner>/code-coverage-api:1.2.3 \
//        webImage=ghcr.io/<owner>/code-coverage-web:1.2.3 databaseKind=PostgreSql \
//        databaseAdminPassword=<secret>
// ------------------------------------------------------------------------------------------------
targetScope = 'resourceGroup'

@description('The Azure region of every resource.')
param location string = resourceGroup().location

@description('A short prefix of the resource names (letters and digits, 3 to 10).')
@minLength(3)
@maxLength(10)
param namePrefix string

@description('The image of the Api, e.g. ghcr.io/<owner>/code-coverage-api:1.2.3.')
param apiImage string

@description('The image of the Web, the same version as the Api.')
param webImage string

@description('The database: PostgreSql (Flexible Server) or SqlServer (Azure SQL).')
@allowed([
  'PostgreSql'
  'SqlServer'
])
param databaseKind string = 'PostgreSql'

@description('The administrator login of the database server.')
param databaseAdminLogin string = 'ccadmin'

@description('The administrator password of the database server; stored in the Key Vault only.')
@secure()
param databaseAdminPassword string

@description('The most replicas of the Api; the Api never goes below one (its job worker).')
@minValue(1)
param apiMaxReplicas int = 3

var suffix = uniqueString(resourceGroup().id, namePrefix)
var vaultName = take('${namePrefix}kv${suffix}', 24)
var databaseName = 'codecoverage'
var postgres = databaseKind == 'PostgreSql'

// Built-in roles: Key Vault Crypto Service Encryption User (wrap and unwrap the Data Protection key)
// and Key Vault Secrets User (read the connection string).
var cryptoUserRole = 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
var secretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-identity'
  location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
  }
}

// The key that protects the Data Protection key ring stored in the database (DataProtection__KeyVaultKeyId).
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

resource cryptoAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, cryptoUserRole)
  scope: vault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cryptoUserRole)
  }
}

resource secretsAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, secretsUserRole)
  scope: vault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRole)
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = if (postgres) {
  name: '${namePrefix}-pg-${suffix}'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    administratorLogin: databaseAdminLogin
    administratorLoginPassword: databaseAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      // Point-in-time restore (ARCHITECTURE §12.5): 7 to 35 days.
      backupRetentionDays: 14
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = if (postgres) {
  parent: postgresServer
  name: databaseName
}

// The Container Apps reach the server through its public endpoint, open to Azure services only.
resource postgresFirewall 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (postgres) {
  parent: postgresServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = if (!postgres) {
  name: '${namePrefix}-sql-${suffix}'
  location: location
  properties: {
    administratorLogin: databaseAdminLogin
    administratorLoginPassword: databaseAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// Azure SQL has READ_COMMITTED_SNAPSHOT on by default: /health/ready checks it at each start.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = if (!postgres) {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
}

resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2021-11-01' = if (!postgres) {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

var connectionString = postgres
  ? 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};Username=${databaseAdminLogin};Password=${databaseAdminPassword};SSL Mode=Require'
  : 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};User ID=${databaseAdminLogin};Password=${databaseAdminPassword};Encrypt=True;TrustServerCertificate=False'

// The connection string lives in the Key Vault; the Container Apps read it by reference.
resource connectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'connection-string'
  properties: {
    value: connectionString
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// The bootstrap values of both hosts (ARCHITECTURE §4bis.2): nothing functional is configured here.
var bootstrap = [
  {
    name: 'ConnectionStrings__CodeCoverage'
    secretRef: 'connection-string'
  }
  {
    name: 'CodeCoverage__Database__Provider'
    value: databaseKind
  }
  {
    name: 'DataProtection__KeyVaultKeyId'
    value: dataProtectionKey.properties.keyUri
  }
  {
    // DefaultAzureCredential picks the user-assigned identity by its client id.
    name: 'AZURE_CLIENT_ID'
    value: identity.properties.clientId
  }
  {
    // Behind the ingress of Container Apps, whose addresses are not known in advance: the Web builds the
    // https callback of the GitHub sign-in from X-Forwarded-Proto.
    name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
    value: 'true'
  }
]

var probes = [
  {
    type: 'Startup'
    httpGet: {
      path: '/health/live'
      port: 8080
    }
    initialDelaySeconds: 5
    periodSeconds: 10
    failureThreshold: 30
  }
  {
    type: 'Liveness'
    httpGet: {
      path: '/health/live'
      port: 8080
    }
    periodSeconds: 30
  }
  {
    // Ready only with the database reachable, the migrations applied and, on SQL Server, RCSI on.
    type: 'Readiness'
    httpGet: {
      path: '/health/ready'
      port: 8080
    }
    periodSeconds: 15
  }
]

var secrets = [
  {
    name: 'connection-string'
    keyVaultUrl: connectionSecret.properties.secretUri
    identity: identity.id
  }
]

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  dependsOn: [
    cryptoAccess
    secretsAccess
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          env: concat(bootstrap, [
            {
              name: 'CodeCoverage__Database__MigrateOnStartup'
              value: 'true'
            }
            {
              name: 'Worker__Enabled'
              value: 'true'
            }
          ])
          probes: probes
        }
      ]
      // Never zero: the Api runs the job worker, which no HTTP request wakes.
      scale: {
        minReplicas: 1
        maxReplicas: apiMaxReplicas
      }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-web'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  dependsOn: [
    api
  ]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        // Blazor Server keeps a circuit per browser: one replica, or sticky sessions.
        stickySessions: {
          affinity: 'sticky'
        }
      }
      secrets: secrets
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: bootstrap
          probes: probes
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

@description('The public address of the Api: Urls:ApiPublicUrl, the webhook and upload host.')
output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'

@description('The public address of the Web console: Urls:WebPublicUrl.')
output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'

@description('The Key Vault that holds the Data Protection key and the connection string.')
output keyVaultName string = vault.name
