// Core Azure resources for K12 Practice. Not deployed yet; review before the first deployment.
// Deploy: az deployment group create -g <resource-group> -f main.bicep -p env=dev

@description('Environment name, used in resource names.')
@allowed(['dev', 'prod'])
param env string = 'dev'

param location string = resourceGroup().location

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

// Next: Azure Database for PostgreSQL Flexible Server, the API container app,
// pipeline jobs, Static Web App for the React app, and Entra External ID.

output appInsightsConnectionString string = insights.properties.ConnectionString
output storageAccountName string = storage.name
output keyVaultName string = vault.name
output containerAppsEnvironmentId string = appsEnv.id
