// Blob Storage: item pictures (read by the API), released-test PDFs (private; publisher copyright, never public),
// and the session cookie's key ring. No public access and no account keys: everyone signs in with Entra.

param name string
param location string
param imagesContainer string
param rawContainer string
param dataProtectionContainer string

@description('The API identity: reads pictures, keeps the key ring.')
param apiPrincipalId string

@description('GitHub Actions: uploads pictures and reads the PDFs during an import.')
param deployerPrincipalId string

@description('The admin group: uploads the PDFs and looks at the pictures.')
param adminGroupObjectId string

import { roles } from 'naming.bicep'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource images 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: imagesContainer
  properties: { publicAccess: 'None' }
}

resource raw 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: rawContainer
  properties: { publicAccess: 'None' }
}

resource dataProtection 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobs
  name: dataProtectionContainer
  properties: { publicAccess: 'None' }
}

var contributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
var reader = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataReader)

resource apiReadsImages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(images.id, apiPrincipalId, reader)
  scope: images
  properties: { roleDefinitionId: reader, principalId: apiPrincipalId, principalType: 'ServicePrincipal' }
}

resource apiKeepsKeyRing 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(dataProtection.id, apiPrincipalId, contributor)
  scope: dataProtection
  properties: { roleDefinitionId: contributor, principalId: apiPrincipalId, principalType: 'ServicePrincipal' }
}

resource deployerWritesImages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(images.id, deployerPrincipalId, contributor)
  scope: images
  properties: { roleDefinitionId: contributor, principalId: deployerPrincipalId }
}

resource deployerReadsRaw 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(raw.id, deployerPrincipalId, reader)
  scope: raw
  properties: { roleDefinitionId: reader, principalId: deployerPrincipalId }
}

resource adminsWriteRaw 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(raw.id, adminGroupObjectId, contributor)
  scope: raw
  properties: { roleDefinitionId: contributor, principalId: adminGroupObjectId, principalType: 'Group' }
}

resource adminsWriteImages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(images.id, adminGroupObjectId, contributor)
  scope: images
  properties: { roleDefinitionId: contributor, principalId: adminGroupObjectId, principalType: 'Group' }
}

output blobEndpoint string = storage.properties.primaryEndpoints.blob
