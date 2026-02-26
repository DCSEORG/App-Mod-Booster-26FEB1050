// main.bicep
// Orchestrates deployment of App Service, Azure SQL, and optionally GenAI resources

@description('Azure region for all resources (except OpenAI which is always swedencentral)')
param location string = 'uksouth'

@description('Object ID of the Entra ID admin for SQL Server')
param adminObjectId string

@description('UPN / login of the Entra ID admin for SQL Server')
param adminLogin string

@description('Whether to deploy Azure OpenAI and AI Search resources')
param deployGenAI bool = false

@description('Day component for managed identity name')
param day string = '26'

@description('Month component for managed identity name')
param month string = '02'

@description('Minute component for managed identity name')
param minute string = '51'

// --- App Service & Managed Identity ---
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    day: day
    month: month
    minute: minute
  }
}

// --- Azure SQL ---
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// --- GenAI (optional) ---
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// --- Outputs ---
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlDatabaseName string = azureSql.outputs.sqlDatabaseName
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityId string = appService.outputs.managedIdentityId
output managedIdentityName string = appService.outputs.managedIdentityName

// GenAI outputs – null-safe operators handle the case where module is not deployed
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint ?? '' : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName ?? '' : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName ?? '' : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint ?? '' : ''
