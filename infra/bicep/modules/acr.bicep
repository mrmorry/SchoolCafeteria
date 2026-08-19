param namePrefix string
param location string
param tags object

// ACR names must be globally unique, alphanumeric only.
var acrName = replace('${namePrefix}acr', '-', '')

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false // pull via managed identity + AcrPull role, never the admin account
  }
}

output loginServer string = acr.properties.loginServer
output acrId string = acr.id
