// The K12 Practice API as a Container App, serving the web app from the same address. Deployed with every release by
// the "Deploy" GitHub workflow, after main.bicep has made the platform. Reads the platform's resources by name.

targetScope = 'resourceGroup'

import { resourceNames } from 'modules/naming.bicep'

@allowed(['dev', 'prod'])
param env string

param location string = resourceGroup().location

@description('The image to run, for example k12devabc.azurecr.io/k12-api:<git sha>.')
param image string

@description('Google OAuth web client id (public). Its authorized JavaScript origins must include this app\'s address.')
param googleClientId string = ''

@description('Emails with owner rights (review screens). Must be Gmail or Google Workspace addresses.')
param owners array = []

@description('While true, only owners and allowedEmails can sign in (private testing). Guests can still practice.')
param inviteOnly bool = true

@description('Families invited during private testing.')
param allowedEmails array = []

@description('0 lets the app sleep when nobody uses it (cheapest; the first visit then takes a few seconds). 1 keeps it warm.')
@minValue(0)
param minReplicas int = 0

@minValue(1)
param maxReplicas int = 2

var names = resourceNames(env, resourceGroup().id)

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: names.identity
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: names.registry
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: names.storage
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: names.keyVault
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' existing = {
  parent: vault
  name: names.dataProtectionKey
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: names.sqlServer
}

resource appsEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: names.appsEnvironment
}

var blobEndpoint = storage.properties.primaryEndpoints.blob

// No password anywhere: the API signs in to SQL as its managed identity. The long timeout covers a paused database
// waking up.
var connectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${names.sqlDatabase};Authentication=Active Directory Managed Identity;User Id=${identity.properties.clientId};Encrypt=True;Connect Timeout=90'

var listSettings = concat(
  map(range(0, length(owners)), i => { name: 'Access__Owners__${i}', value: owners[i] }),
  map(range(0, length(allowedEmails)), i => { name: 'Access__SignIn__AllowedEmails__${i}', value: allowedEmails[i] })
)

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: names.api
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: appsEnv.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        { server: registry.properties.loginServer, identity: identity.id }
      ]
      secrets: [
        {
          name: 'import-key'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/${names.importKeySecret}'
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: concat([
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            // Container Apps ends TLS in front of the app; this makes the app see https and the real client address.
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'ConnectionStrings__K12', value: connectionString }
            { name: 'Content__ImageContainerUri', value: '${blobEndpoint}${names.imagesContainer}' }
            { name: 'DataProtection__KeyRingBlobUri', value: '${blobEndpoint}${names.dataProtectionContainer}/keys.xml' }
            { name: 'DataProtection__KeyVaultKeyUri', value: dataProtectionKey.properties.keyUri }
            { name: 'Auth__Google__ClientId', value: googleClientId }
            { name: 'Access__SignIn__InviteOnly', value: string(inviteOnly) }
            { name: 'Access__ImportKey', secretRef: 'import-key' }
          ], listSettings)
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 2
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          { name: 'http', http: { metadata: { concurrentRequests: '50' } } }
        ]
      }
    }
  }
}

@description('The app\'s address. Add it to the Google client\'s authorized JavaScript origins.')
output url string = 'https://${api.properties.configuration.ingress.fqdn}'
