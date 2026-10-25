// Permify on Azure App Service (Linux container) backed by Azure Database for PostgreSQL Flexible Server.
//
//   App Service (ghcr.io/permify/permify, REST on 443 -> container 3476)
//      ├─ system-assigned identity ──reads──> Key Vault (DB URI + API key secrets)
//      ├─ logs ──> Log Analytics
//      └─ PERMIFY_DATABASE_URI ──> PostgreSQL Flexible Server (database "permify")
//
// Deploy:
//   az group create -n rg-permify -l eastus
//   az deployment group create -g rg-permify -f main.bicep -p main.bicepparam

targetScope = 'resourceGroup'

@description('Prefix for resource names.')
@minLength(3)
@maxLength(12)
param baseName string = 'permify'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Permify container image. Pin a version; avoid "latest" in production.')
param permifyImage string = 'ghcr.io/permify/permify:v1.7.5'

@description('App Service plan SKU. B1 for dev/test, P0v3 or higher for production.')
@allowed(['B1', 'B2', 'B3', 'S1', 'P0v3', 'P1v3', 'P2v3'])
param appServiceSkuName string = 'B1'

@description('Number of App Service instances.')
@minValue(1)
param instanceCount int = 1

@description('PostgreSQL administrator login.')
param postgresAdminLogin string = 'permifyadmin'

@description('PostgreSQL administrator password.')
@secure()
param postgresAdminPassword string

@description('PostgreSQL compute SKU and tier.')
param postgresSkuName string = 'Standard_B1ms'
@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param postgresSkuTier string = 'Burstable'

@description('PostgreSQL storage in GB.')
param postgresStorageGb int = 32

@description('Pre-shared API key that clients send as "Authorization: Bearer <key>". Use a long random value.')
@secure()
@minLength(32)
param permifyApiKey string

@description('Optional CIDR ranges allowed to call Permify (for example your API app\'s outbound IPs). Empty = open to the internet (still protected by the API key).')
param allowedIpRanges array = []

@description('Tags applied to every resource.')
param tags object = {
  app: 'permify'
}

var suffix = uniqueString(resourceGroup().id, baseName)
var names = {
  plan: 'asp-${baseName}-${suffix}'
  site: 'app-${baseName}-${suffix}'
  postgres: 'psql-${baseName}-${suffix}'
  keyVault: take('kv-${baseName}-${suffix}', 24)
  logs: 'log-${baseName}-${suffix}'
}
var databaseName = 'permify'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ---------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.logs
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
// PostgreSQL (Permify's relationship, attribute and schema store)
// ---------------------------------------------------------------------------

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: names.postgres
  location: location
  tags: tags
  sku: {
    name: postgresSkuName
    tier: postgresSkuTier
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    authConfig: {
      passwordAuth: 'Enabled'
      activeDirectoryAuth: 'Disabled'
    }
    storage: {
      storageSizeGB: postgresStorageGb
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 0.0.0.0 - 0.0.0.0 is Azure's special rule for "allow Azure services", which lets App Service reach the server.
// For stricter isolation, use VNet integration + a private endpoint instead (see README).
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------------------------------------------------------------------------
// Key Vault (secrets referenced by App Service settings)
// ---------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.keyVault
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource databaseUriSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'permify-database-uri'
  properties: {
    value: 'postgres://${uriComponent(postgresAdminLogin)}:${uriComponent(postgresAdminPassword)}@${postgres.properties.fullyQualifiedDomainName}:5432/${databaseName}?sslmode=require'
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'permify-api-key'
  properties: {
    value: permifyApiKey
  }
}

// ---------------------------------------------------------------------------
// App Service
// ---------------------------------------------------------------------------

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: names.plan
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: appServiceSkuName
    capacity: instanceCount
  }
  properties: {
    reserved: true // required for Linux
  }
}

var ipRestrictions = [for (cidr, i) in allowedIpRanges: {
  name: 'allow-${i}'
  ipAddress: cidr
  action: 'Allow'
  priority: 100 + i
}]

resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: names.site
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${permifyImage}'
      appCommandLine: 'serve'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/healthz'
      ipSecurityRestrictionsDefaultAction: empty(allowedIpRanges) ? 'Allow' : 'Deny'
      ipSecurityRestrictions: ipRestrictions
    }
  }
}

// Grant the web app's identity read access to the secrets before the settings that reference them are applied.
resource secretsReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, site.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Permify reads its configuration from PERMIFY_<FLAG_NAME> environment variables.
resource appSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: site
  name: 'appsettings'
  dependsOn: [
    secretsReader
    database
    allowAzureServices
  ]
  properties: {
    WEBSITES_PORT: '3476' // Permify REST port; App Service routes 443 to it
    WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'false'
    DOCKER_REGISTRY_SERVER_URL: 'https://ghcr.io'

    PERMIFY_HTTP_PORT: '3476'
    PERMIFY_DATABASE_ENGINE: 'postgres'
    PERMIFY_DATABASE_URI: '@Microsoft.KeyVault(SecretUri=${databaseUriSecret.properties.secretUri})'
    PERMIFY_DATABASE_AUTO_MIGRATE: 'true'

    PERMIFY_AUTHN_ENABLED: 'true'
    PERMIFY_AUTHN_METHOD: 'preshared'
    PERMIFY_AUTHN_PRESHARED_KEYS: '@Microsoft.KeyVault(SecretUri=${apiKeySecret.properties.secretUri})'

    PERMIFY_LOG_LEVEL: 'info'
  }
}

resource siteDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: site
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServicePlatformLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------

@description('Permify REST endpoint. Use as Permify__BaseUrl in the School Calendar API.')
output permifyUrl string = 'https://${site.properties.defaultHostName}'

@description('Web app name (for logs: az webapp log tail -g <rg> -n <name>).')
output webAppName string = site.name

@description('Outbound IPs of the Permify app. Add them to other firewalls if needed.')
output outboundIpAddresses string = site.properties.outboundIpAddresses

@description('Key Vault secret holding the API key. Reference it as Permify__ApiKey from the School Calendar API.')
output apiKeySecretUri string = apiKeySecret.properties.secretUri

output postgresServer string = postgres.properties.fullyQualifiedDomainName
output keyVaultName string = keyVault.name
