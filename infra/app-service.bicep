// app-service.bicep
// Deploys App Service Plan, App Service, and User-Assigned Managed Identity
// Location: UKSOUTH, SKU: S1

@description('Azure region for deployment')
param location string = 'uksouth'

@description('Day component for managed identity name (e.g. 26)')
param day string = '26'

@description('Month component for managed identity name (e.g. 02)')
param month string = '02'

@description('Minute component for managed identity name (e.g. 51)')
param minute string = '51'

// Unique suffix based on resource group id for deterministic naming
var uniqueSuffix = uniqueString(resourceGroup().id)

// Resource names - all lowercase
var managedIdentityName = 'mid-appmodassist-${day}-${month}-${minute}'
var appServicePlanName = 'asp-expensemgmt-${uniqueSuffix}'
var appServiceName = 'app-expensemgmt-${uniqueSuffix}'

// User-Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// App Service Plan - S1 Standard to avoid cold starts
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
