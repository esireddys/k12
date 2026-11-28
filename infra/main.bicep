// K12 Practice platform: everything the API needs except the API app itself (that is app.bicep, deployed with each
// release). Safe to run again: it converges, and it never deletes data.
//
// Run by the "Infrastructure" GitHub workflow. By hand (Windows cmd, one line):
//   az deployment group create -g <resource-group> -n k12-platform -f infra/main.bicep -p env=dev
//     adminName=<you@example.com> adminObjectId=<your object id> importKey=<key>

targetScope = 'resourceGroup'

import { resourceNames } from 'modules/naming.bicep'

@description('Environment name, used in resource names. dev runs on the Visual Studio subscription; prod on pay-as-you-go.')
@allowed(['dev', 'prod'])
param env string

param location string = resourceGroup().location

@description('Who administers the database and uploads content: your Entra sign-in (User), or a group of admins (Group).')
@allowed(['User', 'Group'])
param adminPrincipalType string = 'Group'

@description('The admin\'s sign-in name (for a user, the email you sign in to Azure with) or the group\'s display name.')
param adminName string

@description('Object id of that user or group.')
param adminObjectId string

@description('Object id of the identity that deploys releases (the GitHub Actions managed identity). Defaults to whoever runs this deployment.')
param deployerPrincipalId string = deployer().objectId

@secure()
@minLength(32)
@description('The key the content pipeline sends to import into this environment. Same value as the K12_IMPORT_KEY GitHub secret.')
param importKey string

@description('Use the Azure SQL free offer (one database per subscription).')
param sqlUseFreeLimit bool = true

@description('Log Analytics daily cap in GB.')
param logDailyCapGb int = 1

var names = resourceNames(env, resourceGroup().id)

// The API's own identity, made first so its access is granted before the app exists.
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.identity
  location: location
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    name: names.logs
    location: location
    dailyCapGb: logDailyCapGb
  }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    name: names.registry
    location: location
    apiPrincipalId: apiIdentity.properties.principalId
    deployerPrincipalId: deployerPrincipalId
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: names.storage
    location: location
    imagesContainer: names.imagesContainer
    rawContainer: names.rawContainer
    dataProtectionContainer: names.dataProtectionContainer
    apiPrincipalId: apiIdentity.properties.principalId
    deployerPrincipalId: deployerPrincipalId
    adminObjectId: adminObjectId
    adminPrincipalType: adminPrincipalType
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: names.keyVault
    location: location
    importKeySecretName: names.importKeySecret
    dataProtectionKeyName: names.dataProtectionKey
    apiPrincipalId: apiIdentity.properties.principalId
    importKey: importKey
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: names.sqlServer
    databaseName: names.sqlDatabase
    location: location
    adminName: adminName
    adminObjectId: adminObjectId
    adminPrincipalType: adminPrincipalType
    useFreeLimit: sqlUseFreeLimit
    freeLimitExhaustionBehavior: env == 'prod' ? 'BillOverUsage' : 'AutoPause'
  }
}

module appsEnvironment 'modules/apps-environment.bicep' = {
  name: 'apps-environment'
  params: {
    name: names.appsEnvironment
    location: location
    logsWorkspaceName: monitoring.outputs.name
  }
}

output registryLoginServer string = registry.outputs.loginServer
output storageAccountName string = names.storage
output keyVaultName string = names.keyVault
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output apiIdentityName string = apiIdentity.name
output apiIdentityClientId string = apiIdentity.properties.clientId
