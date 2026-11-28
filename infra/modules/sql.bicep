// Azure SQL Database, serverless: it pauses when idle and resumes on the next request (about a minute).
// Uses the Azure SQL free offer when asked: 100,000 vCore-seconds and 32 GB a month at no charge, one per subscription.
// Entra-only sign-in, no SQL passwords. The API connects with its managed identity (made a database user once, with
// infra/sql/grant-api-identity.sql).

param serverName string
param databaseName string
param location string
param adminGroupName string
param adminGroupObjectId string

@description('Use the Azure SQL free offer for this database.')
param useFreeLimit bool

@description('When the free allowance runs out: pause until next month (dev), or keep running and bill (prod).')
@allowed(['AutoPause', 'BillOverUsage'])
param freeLimitExhaustionBehavior string

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminGroupName
      sid: adminGroupObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
  }
}

// Lets the API (and other Azure services) reach the server. Private networking costs more and is left for later.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
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
    requestedBackupStorageRedundancy: 'Local'
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: useFreeLimit ? freeLimitExhaustionBehavior : null
  }
}

output serverName string = server.name
output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
