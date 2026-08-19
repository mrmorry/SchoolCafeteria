// Deployment option 2 from the brief: Azure App Service using a custom container, one Web App per
// tier (API / frontend), each with a "staging" deployment slot for blue/green releases.
param namePrefix string
param location string
param tags object
param apiImage string
param webImage string
param acrLoginServer string
param keyVaultUri string
param appInsightsConnectionString string

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-plan'
  location: location
  tags: tags
  sku: { name: 'P1v3', tier: 'PremiumV3', capacity: 1 }
  kind: 'linux'
  properties: { reserved: true }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${namePrefix}-api'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${apiImage}'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'ConnectionStrings__Default', value: '@Microsoft.KeyVault(VaultUri=${keyVaultUri};SecretName=ConnectionStrings--Default)' }
        { name: 'Jwt__SigningKey', value: '@Microsoft.KeyVault(VaultUri=${keyVaultUri};SecretName=Jwt--SigningKey)' }
        { name: 'Jwt__Issuer', value: 'SchoolCafeteria' }
        { name: 'Jwt__Audience', value: 'SchoolCafeteria.Clients' }
        { name: 'Storage__Provider', value: 'azure' }
        { name: 'Storage__Azure__ConnectionString', value: '@Microsoft.KeyVault(VaultUri=${keyVaultUri};SecretName=Storage--Azure--ConnectionString)' }
        { name: 'ServiceBus__ConnectionString', value: '@Microsoft.KeyVault(VaultUri=${keyVaultUri};SecretName=ServiceBus--ConnectionString)' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
      ]
    }
  }
}

resource apiStagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: apiApp
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${apiImage}'
      healthCheckPath: '/health/ready'
    }
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${namePrefix}-web'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${webImage}'
      alwaysOn: true
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'WEBSITES_PORT', value: '3000' }
        { name: 'NEXT_PUBLIC_API_BASE_URL', value: 'https://${apiApp.properties.defaultHostName}' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
      ]
    }
  }
}

resource webStagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: webApp
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux,container'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: { linuxFxVersion: 'DOCKER|${acrLoginServer}/${webImage}' }
  }
}

resource acrPullForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(apiApp.id, 'acrpull')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullForWeb 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, 'acrpull')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output webUrl string = 'https://${webApp.properties.defaultHostName}'
output apiPrincipalId string = apiApp.identity.principalId
