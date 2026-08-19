param namePrefix string
param location string
param tags object
param administratorLogin string

@secure()
param administratorPassword string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namePrefix}-sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // restrict via firewall rules / Private Link for production
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'SchoolCafeteria'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_1' // General Purpose Serverless — cost-efficient for an MVP; resize for production load
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
  }
}

resource auditingSettings 'Microsoft.Sql/servers/databases/auditingSettings@2023-08-01-preview' = {
  parent: database
  name: 'default'
  properties: {
    state: 'Disabled' // enable and point at a storage account / Log Analytics before go-live
  }
}

output fullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output sqlServerId string = sqlServer.id
