// azure-sql.bicep
// Deploys Azure SQL Server with Entra ID (AAD) only authentication and an 'expenses' database

@description('Azure region for deployment')
param location string = 'uksouth'

@description('Object ID of the Entra ID administrator')
param adminObjectId string

@description('UPN / login of the Entra ID administrator')
param adminLogin string

@description('Principal ID of the user-assigned managed identity for ##MS_DatabaseManager## permissions')
param managedIdentityPrincipalId string

// Unique suffix from resource group id
var uniqueSuffix = uniqueString(resourceGroup().id)

// All lowercase resource names
var sqlServerName = 'sql-expensemgmt-${uniqueSuffix}'
var sqlDatabaseName = 'expenses'

// Azure SQL Server – Azure AD (Entra ID) only auth
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    // Disable SQL authentication – Entra ID only
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      principalType: 'User'
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Firewall rule: allow all Azure services (0.0.0.0 → 0.0.0.0)
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// expenses database – Basic tier for development
resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

// Outputs
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabaseName
