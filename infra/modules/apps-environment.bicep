// The Container Apps environment (consumption plan: billed per second of use, scales to zero).

param name string
param location string
param logsWorkspaceName string

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logsWorkspaceName
}

resource appsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      { name: 'Consumption', workloadProfileType: 'Consumption' }
    ]
  }
}

output id string = appsEnv.id
output defaultDomain string = appsEnv.properties.defaultDomain
