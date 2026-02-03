@description('Azure region (unused - Bot Service is always global)')
#disable-next-line no-unused-params
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

// Bot Service registration - placeholder for Epic 5 (Teams Approval Workflow)
// Will be configured with Teams channel and Adaptive Card support

var botServiceName = '${baseName}-bot-${environment}'

resource botService 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botServiceName
  location: 'global'
  sku: {
    name: 'S1'
  }
  kind: 'azurebot'
  properties: {
    displayName: 'Arkive Approval Bot'
    description: 'Teams bot for archive approval workflows'
    endpoint: 'https://${baseName}-func-${environment}.azurewebsites.net/api/messages'
    msaAppType: 'SingleTenant'
    msaAppId: '00000000-0000-0000-0000-000000000000' // Placeholder - configure in Epic 5
    msaAppTenantId: '75a7c0d1-4973-4ce9-a2ca-f4a0d9a109ca'
  }
  tags: {
    environment: environment
    epic: '5'
    status: 'placeholder'
  }
}

output botServiceName string = botService.name
output botServiceId string = botService.id
