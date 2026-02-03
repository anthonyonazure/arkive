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

var resourceSuffix = '${baseName}-${environment}'

// SQL Database
module sql 'modules/sql.bicep' = {
  name: 'sql-${resourceSuffix}'
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
  name: 'storage-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Key Vault
module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Service Bus
module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebus-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Monitoring (Application Insights + Log Analytics)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Azure Functions
module functions 'modules/functions.bicep' = {
  name: 'functions-${resourceSuffix}'
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

// Static Web App (Frontend)
module staticwebapp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Bot Service (placeholder for Epic 5)
module bot 'modules/bot.bicep' = {
  name: 'bot-${resourceSuffix}'
  params: {
    location: location
    environment: environment
    baseName: baseName
  }
}

// Role assignments for Managed Identity
module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'roles-${resourceSuffix}'
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
