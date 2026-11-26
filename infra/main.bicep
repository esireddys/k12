// Core Azure resources for K12 Practice. Not deployed yet; review before the first deployment.
// Deploy: az deployment group create -g <resource-group> -f main.bicep \
//   -p env=dev sqlAdminGroupName=<group> sqlAdminGroupObjectId=<object-id>

@description('Environment name, used in resource names.')
@allowed(['dev', 'prod'])
param env string = 'dev'

param location string = resourceGroup().location

@description('Display name of the Entra group that administers the database (for example k12-db-admins).')
param sqlAdminGroupName string

@description('Object id of that Entra group.')
param sqlAdminGroupObjectId string

var prefix = 'k12${env}'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// Question figures and source PDFs. Front Door is added in front of this later.
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${prefix}store${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${prefix}-kv-${take(uniqueString(resourceGroup().id), 6)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
  }
}

// Hosts the API and the pipeline jobs.
resource appsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-apps'
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

// Azure SQL Database, serverless: pauses when idle. Entra-only sign-in, no SQL passwords.
// The API connects with its managed identity (granted as a database user after deployment).
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: '${prefix}-sql-${take(uniqueString(resourceGroup().id), 6)}'
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminGroupName
      sid: sqlAdminGroupObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
  }
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: 'k12'
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
  }
}

// Next: the API container app, pipeline jobs, Static Web App for the React app, and Entra External ID.

output appInsightsConnectionString string = insights.properties.ConnectionString
output storageAccountName string = storage.name
output keyVaultName string = vault.name
output containerAppsEnvironmentId string = appsEnv.id
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDb.name
