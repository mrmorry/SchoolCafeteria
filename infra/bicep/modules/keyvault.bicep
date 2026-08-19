param namePrefix string
param location string
param tags object

@secure()
param sqlConnectionString string

@secure()
param jwtSigningKey string

@secure()
param storageConnectionString string

@secure()
param serviceBusConnectionString string

param appInsightsConnectionString string

var vaultName = take('${namePrefix}-kv', 24)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true // access is granted via Azure RBAC role assignments, not vault access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

resource secretSqlConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ConnectionStrings--Default'
  properties: { value: sqlConnectionString }
}

resource secretJwtSigningKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Jwt--SigningKey'
  properties: { value: jwtSigningKey }
}

resource secretStorageConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Storage--Azure--ConnectionString'
  properties: { value: storageConnectionString }
}

resource secretServiceBusConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ServiceBus--ConnectionString'
  properties: { value: serviceBusConnectionString }
}

resource secretAppInsights 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ApplicationInsights--ConnectionString'
  properties: { value: appInsightsConnectionString }
}

output vaultUri string = keyVault.properties.vaultUri
output vaultName string = keyVault.name
