// Holds the API's container images. Basic tier (about $5 a month). No admin user: the API pulls with its managed
// identity and GitHub Actions pushes with its own sign-in.

param name string
param location string
param apiPrincipalId string
param deployerPrincipalId string

import { roles } from 'naming.bicep'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource apiPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, apiPrincipalId, roles.acrPull)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deployerPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, deployerPrincipalId, roles.acrPush)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPush)
    principalId: deployerPrincipalId
  }
}

output loginServer string = registry.properties.loginServer
