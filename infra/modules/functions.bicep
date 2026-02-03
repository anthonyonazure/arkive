@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

@description('Storage account name for Functions runtime')
param storageAccountName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Key Vault name')
param keyVaultName string

@description('Service Bus namespace')
param serviceBusNamespace string

@description('SQL Server FQDN')
param sqlServerFqdn string

@description('SQL Database name')
param sqlDatabaseName string

var functionAppName = '${baseName}-func-${environment}'
var hostingPlanName = '${baseName}-plan-${environment}'
// Functions runtime storage needs shared key access
var funcStorageName = replace('${baseName}fn${environment}', '-', '')

// Dedicated storage account for Functions runtime (needs shared key)
resource funcStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: funcStorageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
          'https://${baseName}-web-${environment}.azurestaticapps.net'
          'http://localhost:3000'
        ]
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${funcStorage.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${funcStorage.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${funcStorage.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${funcStorage.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVaultName'
          value: keyVaultName
        }
        {
          name: 'ServiceBusConnection__fullyQualifiedNamespace'
          value: '${serviceBusNamespace}.servicebus.windows.net'
        }
        {
          name: 'SqlConnectionString'
          value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;'
        }
        {
          name: 'StorageAccountName'
          value: storageAccountName
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output defaultHostName string = functionApp.properties.defaultHostName
output principalId string = functionApp.identity.principalId
