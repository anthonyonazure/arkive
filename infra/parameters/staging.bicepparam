using '../main.bicep'

param environment = 'staging'
param location = 'eastus'
param baseName = 'arkive'
param sqlAdminObjectId = '00000000-0000-0000-0000-000000000000' // Replace with Entra group Object ID
param sqlAdminLoginName = 'arkive-staging-admins'
