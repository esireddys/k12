// Secrets and keys. RBAC only (no access policies). Holds the content pipeline's import key and the key that wraps
// the session cookie's key ring.

param name string
param location string
param importKeySecretName string
param dataProtectionKeyName string
param apiPrincipalId string

@secure()
@minLength(32)
@description('The key the content pipeline sends to import into this environment (the K12_IMPORT_KEY GitHub secret).')
param importKey string

import { roles } from 'naming.bicep'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource importKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: importKeySecretName
  properties: { value: importKey }
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: dataProtectionKeyName
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

resource apiReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, apiPrincipalId, roles.keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource apiWrapsKeys 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(dataProtectionKey.id, apiPrincipalId, roles.keyVaultCryptoServiceEncryptionUser)
  scope: dataProtectionKey
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultCryptoServiceEncryptionUser)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output vaultUri string = vault.properties.vaultUri
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri
