targetScope = 'resourceGroup'

@description('Environment name (dev, staging, production)')
param environment string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for resource naming')
param baseName string = 'arkive'

@description('SQL admin Entra Object ID')
param sqlAdminObjectId string

@description('SQL admin login name')
param sqlAdminLoginName string = 'arkive-admin'

@description('Azure region for Static Web App (limited availability)')
param staticWebAppLocation string = 'eastus2'

@description('Azure region for Key Vault (may differ if recovered from soft-delete)')
param keyVaultLocation string = location

var deploySuffix = '${baseName}-${environment}'

// SQL Database
module sql 'modules/sql.bicep' = {
  name: 'sql-${deploySuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
    adminObjectId: sqlAdminObjectId
    adminLoginName: sqlAdminLoginName
  }
}

// Storage Account
module storage 'modules/storage.bicep' = {
  name: 'storage-${deploySuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Key Vault (location may differ if recovered from soft-delete)
module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault-${deploySuffix}'
  params: {
    location: keyVaultLocation
    environment: environment
    baseName: baseName
  }
}

// Service Bus
module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebus-${deploySuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Monitoring (Application Insights + Log Analytics)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-${deploySuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Azure Functions
module functions 'modules/functions.bicep' = {
  name: 'functions-${deploySuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
    storageAccountName: storage.outputs.storageAccountName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    keyVaultName: keyvault.outputs.keyVaultName
    serviceBusNamespace: servicebus.outputs.namespaceName
    sqlServerFqdn: sql.outputs.serverFqdn
    sqlDatabaseName: sql.outputs.databaseName
  }
}

// Static Web App (Frontend) - deployed to eastus2 (not available in all regions)
module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-${deploySuffix}'
  params: {
    location: staticWebAppLocation
    environment: environment
    baseName: baseName
  }
}

// Bot Service — skipped for initial deployment (requires real Entra App Registration)
// Uncomment when ready to configure Teams bot in Epic 5:
// module bot 'modules/bot.bicep' = {
//   name: 'bot-${deploySuffix}'
//   params: {
//     location: location
//     environment: environment
//     baseName: baseName
//   }
// }

// Role assignments for Managed Identity
module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'roles-${deploySuffix}'
  params: {
    functionsPrincipalId: functions.outputs.principalId
    storageAccountName: storage.outputs.storageAccountName
    keyVaultName: keyvault.outputs.keyVaultName
    serviceBusNamespaceName: servicebus.outputs.namespaceName
  }
}

// Outputs
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output storageAccountName string = storage.outputs.storageAccountName
output functionsAppName string = functions.outputs.functionAppName
output functionsDefaultHostName string = functions.outputs.defaultHostName
output staticWebAppName string = staticwebapp.outputs.staticWebAppName
output staticWebAppDefaultHostName string = staticwebapp.outputs.defaultHostname
output keyVaultName string = keyvault.outputs.keyVaultName
output serviceBusNamespace string = servicebus.outputs.namespaceName
output appInsightsName string = monitoring.outputs.appInsightsName
