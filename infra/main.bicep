// ClearTrade live demo — designed to run at $0/month:
//  * Container Apps consumption plan, scale-to-zero (stays inside the monthly free grant)
//  * Image pulled from public GitHub Container Registry (no Azure Container Registry)
//  * PostgreSQL hosted on a free external tier (e.g. Neon); passed in as a secret
//  * Log Analytics capped at 0.1 GB/day (well under the 5 GB/month free allowance)
//  * $1 budget alert on the resource group as a safety net
targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base name used for resource names.')
param appName string = 'cleartrade'

@description('Container image to run, e.g. ghcr.io/ommeleven/cleartrade-api:latest')
param image string

@secure()
@description('Npgsql connection string for PostgreSQL. Leave empty to run in in-memory mode.')
param databaseConnectionString string = ''

@secure()
@description('JWT signing key (at least 32 characters).')
param jwtKey string

@secure()
@description('Password for the seeded admin user. Leave empty to skip creating it.')
param adminPassword string = ''

@description('Email address that receives budget alerts.')
param budgetAlertEmail string

@description('First day of the current month (yyyy-MM-01), required by the budget resource.')
param budgetStartDate string

@description('GitHub repository (owner/name) allowed to deploy via OIDC.')
param githubRepo string = 'ommeleven/ClearTrade'

var suffix = uniqueString(resourceGroup().id)
var hasDatabase = !empty(databaseConnectionString)

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs-${suffix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.1') }
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    IngestionMode: 'LogAnalytics'
    SamplingPercentage: 50
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

var baseSecrets = [
  { name: 'jwt-key', value: jwtKey }
  { name: 'appinsights-connection', value: insights.properties.ConnectionString }
]
var dbSecret = hasDatabase ? [{ name: 'db-connection', value: databaseConnectionString }] : []
var adminSecret = empty(adminPassword) ? [] : [{ name: 'admin-password', value: adminPassword }]

var baseEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'Jwt__Key', secretRef: 'jwt-key' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appinsights-connection' }
]
var dbEnv = hasDatabase ? [{ name: 'ConnectionStrings__BrokerageDb', secretRef: 'db-connection' }] : []
var adminEnv = empty(adminPassword) ? [] : [{ name: 'Seed__AdminPassword', secretRef: 'admin-password' }]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-api'
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: concat(baseSecrets, dbSecret, adminSecret)
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: concat(baseEnv, dbEnv, adminEnv)
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              initialDelaySeconds: 3
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        // Scale-to-zero keeps usage inside the Container Apps free grant.
        // A single replica also keeps in-memory mode consistent when no database is configured.
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
}

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: '${appName}-budget'
  properties: {
    category: 'Cost'
    amount: 1
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actualAboveZero: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 1
        contactEmails: [budgetAlertEmail]
        thresholdType: 'Actual'
      }
      forecastAboveBudget: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactEmails: [budgetAlertEmail]
        thresholdType: 'Forecasted'
      }
    }
  }
}

// GitHub Actions deploys with OIDC through this managed identity: no stored credentials, and no
// app registration needed (useful in tenants where users cannot register applications).
resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-github-deploy'
  location: location
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployIdentity
  name: 'github-production'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepo}:environment:production'
    audiences: ['api://AzureADTokenExchange']
  }
}

var contributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

resource deployContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployIdentity.id, contributorRoleId)
  properties: {
    roleDefinitionId: contributorRoleId
    principalId: deployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output deployClientId string = deployIdentity.properties.clientId
output url string = 'https://${app.properties.configuration.ingress.fqdn}'
output containerAppName string = app.name
