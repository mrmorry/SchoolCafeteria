param namePrefix string
param location string
param tags object

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${namePrefix}-sb'
  location: location
  tags: tags
  sku: { name: 'Basic' }
}

resource notificationsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'notifications'
  properties: {
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P1D'
  }
}

resource integrationEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'integration-events'
  properties: {
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
  }
}

resource rootAuthRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' existing = {
  parent: serviceBusNamespace
  name: 'RootManageSharedAccessKey'
}

output namespaceName string = serviceBusNamespace.name
output primaryConnectionString string = rootAuthRule.listKeys().primaryConnectionString
