// Orchestrates all Azure resources for SchoolCafeteria. Supports two hosting targets for the
// application containers, selected by `hostingModel`:
//   - 'appservice'    : Azure App Service (Linux, custom container) with staging/production slots.
//   - 'containerapps'  : Azure Container Apps (revision-based, scale-to-N).
// Both consume the same images published to the Azure Container Registry provisioned here.
targetScope = 'resourceGroup'

@description('Short environment name, e.g. dev, staging, prod. Used as a naming suffix.')
@minLength(2)
@maxLength(10)
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Hosting model for the API and web containers.')
@allowed(['appservice', 'containerapps'])
param hostingModel string = 'appservice'

@description('SQL Server administrator login.')
param sqlAdminLogin string = 'schoolcafeteria_admin'

@description('SQL Server administrator password. Pass via a secure pipeline parameter, never committed.')
@secure()
param sqlAdminPassword string

@description('JWT signing key. Pass via a secure pipeline parameter; stored in Key Vault, never in app settings directly.')
@secure()
param jwtSigningKey string

@description('Container image tag to deploy (e.g. the CI build number or git SHA).')
param imageTag string = 'latest'

@description('Full image repository for the API, e.g. myacr.azurecr.io/schoolcafeteria-api.')
param apiImage string

@description('Full image repository for the web frontend, e.g. myacr.azurecr.io/schoolcafeteria-web.')
param webImage string

var resourcePrefix = 'sccaf-${environmentName}'
var tags = {
  application: 'SchoolCafeteria'
  environment: environmentName
  managedBy: 'bicep'
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
    administratorLogin: sqlAdminLogin
    administratorPassword: sqlAdminPassword
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'serviceBus'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
    sqlConnectionString: 'Server=tcp:${sql.outputs.fullyQualifiedDomainName},1433;Database=${sql.outputs.databaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=false;'
    jwtSigningKey: jwtSigningKey
    storageConnectionString: storage.outputs.primaryConnectionString
    serviceBusConnectionString: serviceBus.outputs.primaryConnectionString
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

module appService 'modules/appservice.bicep' = if (hostingModel == 'appservice') {
  name: 'appService'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
    apiImage: '${apiImage}:${imageTag}'
    webImage: '${webImage}:${imageTag}'
    acrLoginServer: acr.outputs.loginServer
    keyVaultUri: keyVault.outputs.vaultUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

module containerApps 'modules/containerapps.bicep' = if (hostingModel == 'containerapps') {
  name: 'containerApps'
  params: {
    namePrefix: resourcePrefix
    location: location
    tags: tags
    apiImage: '${apiImage}:${imageTag}'
    webImage: '${webImage}:${imageTag}'
    acrLoginServer: acr.outputs.loginServer
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    keyVaultUri: keyVault.outputs.vaultUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Grant the compute identity (whichever hosting model was chosen) read access to Key Vault secrets.
module keyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'keyVaultAccess'
  params: {
    keyVaultName: keyVault.outputs.vaultName
    principalId: hostingModel == 'appservice' ? appService.outputs.apiPrincipalId : containerApps.outputs.apiPrincipalId
  }
}

output resourceGroupName string = resourceGroup().name
output acrLoginServer string = acr.outputs.loginServer
output sqlServerFqdn string = sql.outputs.fullyQualifiedDomainName
output keyVaultUri string = keyVault.outputs.vaultUri
output apiUrl string = hostingModel == 'appservice' ? appService.outputs.apiUrl : containerApps.outputs.apiUrl
output webUrl string = hostingModel == 'appservice' ? appService.outputs.webUrl : containerApps.outputs.webUrl
