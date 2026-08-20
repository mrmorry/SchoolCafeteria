// Deployment option 3 from the brief: Azure Container Apps. Same images as the App Service path,
// running under a shared Container Apps Environment with revision-based traffic and scale-to-1 (or
// scale-to-0 for the frontend if desired) instead of an always-on App Service Plan.
param namePrefix string
param location string
param tags object
param apiImage string
param webImage string
param acrLoginServer string
param logAnalyticsWorkspaceId string
param keyVaultUri string
param appInsightsConnectionString string

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: { external: false, targetPort: 8080, transport: 'http' }
      registries: [{ server: acrLoginServer, identity: 'system' }]
      activeRevisionsMode: 'Single'
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acrLoginServer}/${apiImage}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'Jwt__Issuer', value: 'SchoolCafeteria' }
            { name: 'Jwt__Audience', value: 'SchoolCafeteria.Clients' }
            { name: 'Storage__Provider', value: 'azure' }
            { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
            { name: 'KeyVault__Uri', value: keyVaultUri } // app resolves Key Vault-backed settings at startup via managed identity
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080 }, periodSeconds: 15 }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 5 }
    }
  }
}

resource webApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-web'
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: { external: true, targetPort: 3000, transport: 'http' }
      registries: [{ server: acrLoginServer, identity: 'system' }]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${acrLoginServer}/${webImage}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'NEXT_PUBLIC_API_BASE_URL', value: 'https://${apiApp.properties.configuration.ingress.fqdn}' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 3 }
    }
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

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${webApp.properties.configuration.ingress.fqdn}'
output apiPrincipalId string = apiApp.identity.principalId
