@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

var namespaceName = '${baseName}-sb-${environment}'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// Queue: scan-jobs (maxConcurrentCalls=3)
resource scanJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'scan-jobs'
  properties: {
    maxDeliveryCount: 5
    defaultMessageTimeToLive: 'P1D'
    lockDuration: 'PT5M'
    deadLetteringOnMessageExpiration: true
  }
}

// Queue: archive-jobs (maxConcurrentCalls=10)
resource archiveJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'archive-jobs'
  properties: {
    maxDeliveryCount: 5
    defaultMessageTimeToLive: 'P1D'
    lockDuration: 'PT5M'
    deadLetteringOnMessageExpiration: true
  }
}

// Queue: retrieval-jobs (maxConcurrentCalls=5)
resource retrievalJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'retrieval-jobs'
  properties: {
    maxDeliveryCount: 5
    defaultMessageTimeToLive: 'P1D'
    lockDuration: 'PT5M'
    deadLetteringOnMessageExpiration: true
  }
}

// Queue: notification-jobs (maxConcurrentCalls=10)
resource notificationJobsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'notification-jobs'
  properties: {
    maxDeliveryCount: 5
    defaultMessageTimeToLive: 'P1D'
    lockDuration: 'PT5M'
    deadLetteringOnMessageExpiration: true
  }
}

// Queue: scan-completed (event queue for downstream consumers like analytics)
resource scanCompletedQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'scan-completed'
  properties: {
    maxDeliveryCount: 3
    defaultMessageTimeToLive: 'P1D'
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

output namespaceName string = serviceBusNamespace.name
output namespaceId string = serviceBusNamespace.id
