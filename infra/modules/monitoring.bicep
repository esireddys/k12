// Logs from the container apps. A daily cap keeps a runaway log loop from costing money.

param name string
param location string

@description('Daily ingestion cap in GB. Logging stops for the day when it is reached.')
param dailyCapGb int

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: dailyCapGb }
  }
}

output id string = logs.id
output name string = logs.name
