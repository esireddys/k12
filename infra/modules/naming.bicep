// One place for resource names, so the platform (main.bicep) and the app (app.bicep) always agree.
// Names include a short hash of the resource group id: unique per subscription and resource group, stable across runs.

@export()
@description('Names for every K12 Practice resource in one environment.')
func resourceNames(env string, resourceGroupId string) object => {
  logs: 'k12${env}-logs'
  identity: 'k12${env}-api-id'
  registry: 'k12${env}${take(uniqueString(resourceGroupId), 8)}'
  storage: 'k12${env}${take(uniqueString(resourceGroupId), 10)}'
  keyVault: 'k12${env}-kv-${take(uniqueString(resourceGroupId), 6)}'
  appsEnvironment: 'k12${env}-apps'
  sqlServer: 'k12${env}-sql-${take(uniqueString(resourceGroupId), 6)}'
  sqlDatabase: 'k12'
  api: 'k12${env}-api'
  // Blob containers: item pictures the API serves, the released-test PDFs (private, publisher copyright), and the
  // key ring that protects the session cookie.
  imagesContainer: 'items'
  rawContainer: 'raw'
  dataProtectionContainer: 'dataprotection'
  importKeySecret: 'import-key'
  dataProtectionKey: 'dataprotection'
}

@export()
@description('Built-in Azure role ids used here.')
var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  acrPush: '8311e382-0749-4cb8-b61a-304f252e45ec'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageBlobDataReader: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultCryptoServiceEncryptionUser: 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
}
