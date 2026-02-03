@description('Azure region')
param location string

@description('Environment name')
param environment string

@description('Base name for resources')
param baseName string

var logAnalyticsName = '${baseName}-log-${environment}'
var appInsightsName = '${baseName}-ai-${environment}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 30
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Alert: Function failures > 5 in 5 minutes
resource failureAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${baseName}-func-failures-${environment}'
  location: 'global'
  properties: {
    severity: 2
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighFailureRate'
          metricName: 'exceptions/count'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
  }
}

output logAnalyticsId string = logAnalytics.id
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
