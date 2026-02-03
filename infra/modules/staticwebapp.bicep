@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

var staticWebAppName = '${baseName}-web-${environment}'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      appLocation: '/src/arkive-web'
      outputLocation: '.next'
    }
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppId string = staticWebApp.id
