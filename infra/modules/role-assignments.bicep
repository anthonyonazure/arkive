@description('Functions app Managed Identity principal ID')
param functionsPrincipalId string

@description('Storage account name')
param storageAccountName string

@description('Key Vault name')
param keyVaultName string

@description('Service Bus namespace name')
param serviceBusNamespaceName string

// Built-in role definition IDs
var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'
var serviceBusSenderRole = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusReceiverRole = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

// Reference existing resources
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

// Functions → Blob Storage: Storage Blob Data Contributor
resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionsPrincipalId, storageBlobDataContributorRole)
  scope: storageAccount
  properties: {
    principalId: functionsPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRole)
    principalType: 'ServicePrincipal'
  }
}

// Functions → Key Vault: Key Vault Secrets User
resource keyVaultSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionsPrincipalId, keyVaultSecretsUserRole)
  scope: keyVault
  properties: {
    principalId: functionsPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalType: 'ServicePrincipal'
  }
}

// Functions → Service Bus: Sender
resource serviceBusSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, functionsPrincipalId, serviceBusSenderRole)
  scope: serviceBusNamespace
  properties: {
    principalId: functionsPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusSenderRole)
    principalType: 'ServicePrincipal'
  }
}

// Functions → Service Bus: Receiver
resource serviceBusReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, functionsPrincipalId, serviceBusReceiverRole)
  scope: serviceBusNamespace
  properties: {
    principalId: functionsPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusReceiverRole)
    principalType: 'ServicePrincipal'
  }
}
