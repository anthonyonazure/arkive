@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

@description('Entra ID Object ID for SQL admin')
param adminObjectId string

@description('SQL admin login name')
param adminLoginName string

var serverName = '${baseName}-sql-${environment}'
var databaseName = '${baseName}-db-${environment}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLoginName
      sid: adminObjectId
      tenantId: subscription().tenantId
      principalType: 'Group'
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_1'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368 // 32 GB
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// Allow Azure services
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
output serverId string = sqlServer.id
