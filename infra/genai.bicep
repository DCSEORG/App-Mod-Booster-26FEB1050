// genai.bicep
// Deploys Azure OpenAI (GPT-4o in swedencentral) and AI Search (S0)
// Assigns "Cognitive Services OpenAI User" role to the supplied managed identity

@description('Principal ID of the user-assigned managed identity')
param managedIdentityPrincipalId string

// Unique suffix – all resource names lowercase
var uniqueSuffix = toLower(uniqueString(resourceGroup().id))

var openAIName = 'aoai-expensemgmt-${uniqueSuffix}'
var searchName = 'srch-expensemgmt-${uniqueSuffix}'

// Azure OpenAI – must be deployed to swedencentral
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: 'swedencentral'
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o model deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
    }
  }
}

// AI Search – S0 SKU
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: 'uksouth'
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
  }
}

// "Cognitive Services OpenAI User" built-in role
// Role definition ID: 5e0bd9bd-7b93-4f28-af87-19fc36ad61bd
resource openAIUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = gpt4oDeployment.name
output openAIName string = openAI.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
