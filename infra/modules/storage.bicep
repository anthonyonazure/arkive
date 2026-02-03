@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

// Storage account names must be 3-24 chars, lowercase alphanumeric only
var storageAccountName = replace('${baseName}st${environment}', '-', '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 30
    }
  }
}

// Lifecycle management for tiered storage
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'MoveToCoolAfter30Days'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 30
                }
              }
            }
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['archives/']
            }
          }
        }
        {
          name: 'MoveToColdAfter90Days'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCold: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['archives/']
            }
          }
        }
        {
          name: 'MoveToArchiveAfter180Days'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToArchive: {
                  daysAfterModificationGreaterThan: 180
                }
              }
            }
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['archives/']
            }
          }
        }
      ]
    }
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
