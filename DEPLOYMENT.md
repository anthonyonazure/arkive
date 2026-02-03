# Arkive Deployment Guide

Step-by-step instructions for deploying Arkive to Azure. This guide assumes you have an Azure subscription and basic familiarity with the Azure Portal.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Create Azure Resource Group & Security Group](#2-create-azure-resource-group--security-group)
3. [Update Bicep Parameters](#3-update-bicep-parameters)
4. [Deploy Azure Infrastructure](#4-deploy-azure-infrastructure)
5. [Register Entra ID Application](#5-register-entra-id-application)
6. [Run Database Migrations](#6-run-database-migrations)
7. [Deploy the Backend (Azure Functions)](#7-deploy-the-backend-azure-functions)
8. [Deploy the Frontend (Static Web Apps)](#8-deploy-the-frontend-static-web-apps)
9. [Configure Key Vault Secrets](#9-configure-key-vault-secrets)
10. [Configure Frontend Environment Variables](#10-configure-frontend-environment-variables)
11. [Configure CORS (Cross-Origin Requests)](#11-configure-cors-cross-origin-requests)
12. [Verify the Deployment](#12-verify-the-deployment)
13. [Troubleshooting](#13-troubleshooting)
14. [Redeployment (Updating Code)](#14-redeployment-updating-code)

---

## 1. Prerequisites

Install these tools on your local machine before starting:

```powershell
# Azure CLI
winget install Microsoft.AzureCLI

# .NET 8 SDK (to build the backend and run migrations)
winget install Microsoft.DotNet.SDK.8

# Node.js 20+ (to build the frontend)
winget install OpenJS.NodeJS.LTS

# Azure Functions Core Tools (to test locally and deploy)
winget install Microsoft.Azure.FunctionsCoreTools

# GitHub CLI (already installed)
# winget install GitHub.cli
```

After installing, verify each tool:

```powershell
az --version          # Should show 2.x+
dotnet --version      # Should show 8.x
node --version        # Should show 20.x+
func --version        # Should show 4.x
gh --version          # Should show 2.x
```

Log in to Azure CLI:

```powershell
az login
```

This opens a browser for authentication. After login, set your subscription:

```powershell
# List your subscriptions
az account list --output table

# Set the one you want to use
az account set --subscription "YOUR-SUBSCRIPTION-ID"
```

---

## 2. Create Azure Resource Group & Security Group

### 2a. Create a Resource Group

All Arkive resources will live in one resource group. Use `eastus2` — some Azure services (Static Web Apps, SQL) have limited availability in `eastus`:

```powershell
az group create --name rg-arkive-dev --location eastus2
```

### 2b. Create an Entra ID Security Group for SQL Admin

Azure SQL uses Entra ID authentication (no passwords). You need a security group that will be the SQL admin:

```powershell
# Create the security group
az ad group create --display-name "arkive-dev-admins" --mail-nickname "arkive-dev-admins"

# Get the group's Object ID (you'll need this for the Bicep parameters)
az ad group show --group "arkive-dev-admins" --query id --output tsv
```

**Save the Object ID** -- it looks like `a1b2c3d4-e5f6-7890-abcd-ef1234567890`.

### 2c. Add Yourself to the SQL Admin Group

```powershell
# Get your own Object ID
az ad signed-in-user show --query id --output tsv

# Add yourself to the group
az ad group member add --group "arkive-dev-admins" --member-id "YOUR-USER-OBJECT-ID"
```

---

## 3. Update Bicep Parameters

Edit the parameter file `infra/parameters/dev.bicepparam`:

```bicep
using '../main.bicep'

param environment = 'dev'
param location = 'eastus2'
param baseName = 'arkive'
param sqlAdminObjectId = 'PASTE-YOUR-GROUP-OBJECT-ID-HERE'
param sqlAdminLoginName = 'arkive-dev-admins'
param keyVaultLocation = 'eastus2'
```

Replace `PASTE-YOUR-GROUP-OBJECT-ID-HERE` with the Object ID from Step 2b.

**Note on `keyVaultLocation`:** If you previously deployed and deleted the resource group, Key Vault may be in a "soft-deleted" state in its original region. You'll need to either purge it (`az keyvault purge --name arkive-kv-dev`) or recover it (`az keyvault recover --name arkive-kv-dev`) and set `keyVaultLocation` to the region where the vault was recovered.

---

## 4. Deploy Azure Infrastructure

This single command provisions all Azure resources (SQL, Storage, Key Vault, Functions, Service Bus, Static Web App, Application Insights):

```powershell
az deployment group create `
  --resource-group rg-arkive-dev `
  --name arkive-deploy `
  --template-file infra/main.bicep `
  --parameters infra/parameters/dev.bicepparam
```

This runs for several minutes. When complete, it outputs the resource names. **Save these outputs** -- you'll need them:

```powershell
# View the deployment outputs anytime
az deployment group show `
  --resource-group rg-arkive-dev `
  --name arkive-deploy `
  --query properties.outputs
```

**Note:** If the deployment partially fails (e.g., ARM transient errors), just re-run the same command. Bicep deployments are idempotent — already-created resources are updated, missing ones are created.

Key outputs you'll reference later:

| Output | Example Value | Used For |
|--------|-------------|----------|
| `functionsAppName` | `arkive-func-dev` | Deploying backend code |
| `functionsDefaultHostName` | `arkive-func-dev.azurewebsites.net` | Frontend API URL |
| `staticWebAppName` | `arkive-web-dev` | Deploying frontend code |
| `staticWebAppDefaultHostName` | `kind-plant-0fbd74b0f.4.azurestaticapps.net` | Your app's URL |
| `sqlServerFqdn` | `arkive-sql-dev.database.windows.net` | Database migrations |
| `sqlDatabaseName` | `arkive-db-dev` | Database migrations |
| `keyVaultName` | `arkive-kv-dev` | Storing secrets |

---

## 5. Register Entra ID Application

The Entra ID app registration handles user authentication and Graph API permissions.

### 5a. Create the App Registration

```powershell
# Create the app registration
az ad app create `
  --display-name "Arkive Dev" `
  --sign-in-audience "AzureADMyOrg" `
  --web-redirect-uris "https://YOUR-STATIC-WEB-APP-HOSTNAME" "http://localhost:3000" `
  --enable-id-token-issuance true `
  --enable-access-token-issuance true
```

Replace `YOUR-STATIC-WEB-APP-HOSTNAME` with the `staticWebAppDefaultHostName` from Step 4 (e.g., `https://arkive-web-dev.azurestaticapps.net`).

```powershell
# Get the Application (client) ID
az ad app list --display-name "Arkive Dev" --query "[0].appId" --output tsv

# Get your Entra Tenant ID
az account show --query tenantId --output tsv
```

**Save both values** -- you'll need them for frontend configuration.

### 5b. Create a Client Secret

```powershell
az ad app credential reset `
  --id "YOUR-APP-CLIENT-ID" `
  --display-name "arkive-dev-secret" `
  --years 2
```

**Save the password value** -- you'll store it in Key Vault. It's only shown once.

### 5c. Expose an API Scope

This allows the frontend to request tokens:

1. Go to **Azure Portal > App registrations > Arkive Dev > Expose an API**
2. Click **Set** next to "Application ID URI" -- accept the default (`api://YOUR-CLIENT-ID`)
3. Click **Add a scope**:
   - Scope name: `access_as_user`
   - Who can consent: **Admins and users**
   - Admin consent display name: "Access Arkive as user"
   - Admin consent description: "Allow the app to access Arkive on behalf of the signed-in user"
   - State: **Enabled**

### 5d. Add Graph API Permissions (for SharePoint scanning)

1. Go to **Azure Portal > App registrations > Arkive Dev > API permissions**
2. Click **Add a permission > Microsoft Graph > Application permissions**
3. Add these permissions:
   - `Sites.Selected` (allows targeted site access)
   - `User.Read.All` (for Teams notification delivery)
4. Click **Grant admin consent** for your directory

### 5e. Create a Service Principal

```powershell
az ad sp create --id "YOUR-APP-CLIENT-ID"
```

### 5f. Define App Roles

The backend uses role-based access control. Add these roles via Azure Portal:

1. Go to **App registrations > Arkive Dev > App roles**
2. Create these roles:

| Display Name | Value | Allowed Members | Description |
|-------------|-------|-----------------|-------------|
| Platform Admin | `Platform.Admin` | Users/Groups | Full platform access |
| MSP Admin | `Msp.Admin` | Users/Groups | MSP organization admin |
| MSP Tech | `Msp.Tech` | Users/Groups | MSP technician |

---

## 6. Run Database Migrations

The database schema needs to be applied to your Azure SQL instance.

### 6a. Install EF Core Tools

```powershell
dotnet tool install --global dotnet-ef
```

### 6b. Allow Your IP Through SQL Firewall

```powershell
# Get your public IP
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org")

# Add firewall rule
az sql server firewall-rule create `
  --resource-group rg-arkive-dev `
  --server arkive-sql-dev `
  --name "MyIP" `
  --start-ip-address $myIp `
  --end-ip-address $myIp
```

### 6c. Run Migrations

```powershell
# Build the project first
dotnet build src/Arkive.Functions/Arkive.Functions.csproj

# Run migrations against Azure SQL
# The connection string uses your Entra ID login (since you're in the admin group)
dotnet ef database update `
  --project src/Arkive.Data/Arkive.Data.csproj `
  --startup-project src/Arkive.Functions/Arkive.Functions.csproj `
  --connection "Server=tcp:arkive-sql-dev.database.windows.net,1433;Database=arkive-db-dev;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;"
```

Replace `arkive-sql-dev` and `arkive-db-dev` with your actual values from Step 4.

You should see output like:
```
Applying migration '20260202022030_InitialCreate'.
Applying migration '20260202022051_AddRowLevelSecurity'.
Applying migration '20260202100000_AddConnectedAtToClientTenant'.
...
Done.
```

### 6d. Grant Functions App Database Access

The Azure Functions app uses Managed Identity to connect to SQL. You need to grant it access:

```powershell
# Get the Functions app's Managed Identity Object ID
az functionapp identity show `
  --name arkive-func-dev `
  --resource-group rg-arkive-dev `
  --query principalId --output tsv
```

Then connect to Azure SQL (using Azure Portal Query Editor or Azure Data Studio) and run:

```sql
-- Replace 'arkive-func-dev' with your actual Functions app name
CREATE USER [arkive-func-dev] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [arkive-func-dev];
ALTER ROLE db_datawriter ADD MEMBER [arkive-func-dev];

-- Grant execute on RLS functions (needed for SESSION_CONTEXT)
GRANT EXECUTE ON SCHEMA :: dbo TO [arkive-func-dev];
```

---

## 7. Deploy the Backend (Azure Functions)

### 7a. Build and Publish

```powershell
# Publish the Functions app (creates a deployable package)
dotnet publish src/Arkive.Functions/Arkive.Functions.csproj `
  --configuration Release `
  --output ./publish/functions
```

### 7b. Deploy to Azure

```powershell
# Zip the published output
Compress-Archive -Path ./publish/functions/* -DestinationPath ./publish/functions.zip -Force

# Deploy via Azure CLI
az functionapp deployment source config-zip `
  --resource-group rg-arkive-dev `
  --name arkive-func-dev `
  --src ./publish/functions.zip
```

### 7c. Configure App Settings for Entra ID

```powershell
az functionapp config appsettings set `
  --name arkive-func-dev `
  --resource-group rg-arkive-dev `
  --settings `
    "EntraId:TenantId=YOUR-ENTRA-TENANT-ID" `
    "EntraId:ClientId=YOUR-APP-CLIENT-ID" `
    "EntraId:Audience=api://YOUR-APP-CLIENT-ID"
```

### 7d. Verify Backend is Running

```powershell
# Check the health endpoint
curl https://arkive-func-dev.azurewebsites.net/api/health
```

You should get a 200 response.

---

## 8. Deploy the Frontend (Static Web Apps)

### 8a. Get the Static Web App Deployment Token

```powershell
az staticwebapp secrets list `
  --name arkive-web-dev `
  --resource-group rg-arkive-dev `
  --query properties.apiKey --output tsv
```

**Save this token** -- it's used for deployment.

### 8b. Install the SWA CLI

```powershell
npm install -g @azure/static-web-apps-cli
```

### 8c. Build the Frontend

```powershell
cd src/arkive-web
npm install
npm run build
```

### 8d. Deploy

```powershell
swa deploy `
  --app-location ./src/arkive-web `
  --output-location .next `
  --deployment-token "YOUR-DEPLOYMENT-TOKEN"
```

Alternatively, connect your GitHub repo for automatic deployments:

```powershell
az staticwebapp update `
  --name arkive-web-dev `
  --resource-group rg-arkive-dev `
  --source https://github.com/anthonyonazure/arkive `
  --branch main `
  --app-location "/src/arkive-web" `
  --output-location ".next" `
  --token "YOUR-GITHUB-PAT"
```

---

## 9. Configure Key Vault Secrets

Store sensitive configuration in Key Vault. The Functions app reads these automatically via Managed Identity.

```powershell
$kvName = "arkive-kv-dev"  # Replace with your Key Vault name from Step 4

# Entra ID client secret (from Step 5b)
az keyvault secret set --vault-name $kvName --name "EntraId--ClientSecret" --value "YOUR-CLIENT-SECRET"

# SQL connection string (Managed Identity -- already set via Bicep app settings)
# No action needed -- the Functions app uses the connection string from Bicep

# Bot Framework credentials (for Teams integration - Epic 5)
# az keyvault secret set --vault-name $kvName --name "BotFramework--AppId" --value "YOUR-BOT-APP-ID"
# az keyvault secret set --vault-name $kvName --name "BotFramework--AppPassword" --value "YOUR-BOT-SECRET"
```

Also set the Key Vault URI in the Functions app settings:

```powershell
az functionapp config appsettings set `
  --name arkive-func-dev `
  --resource-group rg-arkive-dev `
  --settings "KeyVault:VaultUri=https://$kvName.vault.azure.net/"
```

---

## 10. Configure Frontend Environment Variables

Static Web Apps uses environment variables for the frontend. Configure them in the Azure Portal or via CLI:

### 10a. Via Azure Portal

1. Go to **Static Web Apps > arkive-web-dev > Configuration**
2. Add these application settings:

| Name | Value |
|------|-------|
| `NEXT_PUBLIC_ENTRA_CLIENT_ID` | Your App Client ID from Step 5a |
| `NEXT_PUBLIC_ENTRA_TENANT_ID` | Your Entra Tenant ID from Step 5a |
| `NEXT_PUBLIC_API_BASE_URL` | `https://arkive-func-dev.azurewebsites.net/api` |
| `NEXT_PUBLIC_REDIRECT_URI` | `https://arkive-web-dev.azurestaticapps.net` |

### 10b. Via CLI

```powershell
az staticwebapp appsettings set `
  --name arkive-web-dev `
  --resource-group rg-arkive-dev `
  --setting-names `
    "NEXT_PUBLIC_ENTRA_CLIENT_ID=YOUR-APP-CLIENT-ID" `
    "NEXT_PUBLIC_ENTRA_TENANT_ID=YOUR-ENTRA-TENANT-ID" `
    "NEXT_PUBLIC_API_BASE_URL=https://arkive-func-dev.azurewebsites.net/api" `
    "NEXT_PUBLIC_REDIRECT_URI=https://arkive-web-dev.azurestaticapps.net"
```

After setting these, **rebuild and redeploy the frontend** (Step 8) for the changes to take effect.

---

## 11. Configure CORS (Cross-Origin Requests)

The frontend (Static Web App) makes API calls to the backend (Azure Functions). You need to allow the frontend's origin:

```powershell
# Allow the Static Web App to call the Functions API
az functionapp cors add `
  --name arkive-func-dev `
  --resource-group rg-arkive-dev `
  --allowed-origins "https://kind-plant-0fbd74b0f.4.azurestaticapps.net"

# Also allow localhost for development
az functionapp cors add `
  --name arkive-func-dev `
  --resource-group rg-arkive-dev `
  --allowed-origins "http://localhost:3000"
```

Replace the Static Web App URL with your actual `staticWebAppDefaultHostName` from Step 4 (prefixed with `https://`).

---

## 12. Verify the Deployment

### 12a. Check Azure Resources

```powershell
# List all resources in the resource group
az resource list --resource-group rg-arkive-dev --output table
```

You should see approximately 10+ resources (SQL server, database, storage accounts, function app, static web app, key vault, service bus, app insights, etc.).

### 12b. Test the Backend API

```powershell
# Health check (no auth required)
curl https://arkive-func-dev.azurewebsites.net/api/health

# Should return: {"data":{"status":"healthy","timestamp":"..."}}
```

### 12c. Test the Frontend

Open your browser and navigate to:
```
https://arkive-web-dev.azurestaticapps.net
```

You should see the Arkive login page. Clicking "Sign In" should redirect to Microsoft Entra ID login.

### 12d. Create Your First MSP Organization

After logging in, if you see "No organization found", you need to create one. Use the Platform Admin API:

```powershell
# Get an access token
$token = az account get-access-token --resource "api://YOUR-CLIENT-ID" --query accessToken --output tsv

# Create an MSP organization
curl -X POST "https://arkive-func-dev.azurewebsites.net/api/v1/organizations" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{"name": "My MSP Organization", "entraIdTenantId": "YOUR-TENANT-ID"}'
```

---

## 13. Troubleshooting

### "Function app returns 500 errors"

Check the logs:
```powershell
az functionapp log tail --name arkive-func-dev --resource-group rg-arkive-dev
```

Common causes:
- Missing app settings (EntraId:TenantId, etc.)
- Database connection failed (check firewall rules, Managed Identity access)
- Key Vault access denied (check role assignments)

### "Database migration fails with auth error"

Ensure you're in the SQL admin group (Step 2c) and that your Azure CLI is logged in:
```powershell
az login
az account show  # Verify correct account
```

### "Frontend shows blank page or auth errors"

Check browser developer console (F12). Common causes:
- `NEXT_PUBLIC_ENTRA_CLIENT_ID` not set or wrong
- Redirect URI mismatch (must exactly match what's in App Registration)
- API scope `access_as_user` not exposed (Step 5c)

### "Static Web App deployment fails"

Ensure you're using the correct deployment token:
```powershell
az staticwebapp secrets list --name arkive-web-dev --resource-group rg-arkive-dev
```

### "Service Bus / Queue triggers not firing"

The Functions app uses Managed Identity for Service Bus. Verify the role assignment:
```powershell
az role assignment list `
  --assignee $(az functionapp identity show --name arkive-func-dev --resource-group rg-arkive-dev --query principalId --output tsv) `
  --scope $(az servicebus namespace show --name arkive-sb-dev --resource-group rg-arkive-dev --query id --output tsv) `
  --output table
```

It should show "Azure Service Bus Data Owner" or "Azure Service Bus Data Sender + Receiver".

### "Key Vault access denied"

```powershell
# Check role assignments on Key Vault
az role assignment list `
  --scope $(az keyvault show --name arkive-kv-dev --resource-group rg-arkive-dev --query id --output tsv) `
  --output table
```

The Functions Managed Identity needs "Key Vault Secrets User" role.

### Useful Diagnostic Commands

```powershell
# View all Function App settings
az functionapp config appsettings list --name arkive-func-dev --resource-group rg-arkive-dev --output table

# View Static Web App settings
az staticwebapp appsettings list --name arkive-web-dev --resource-group rg-arkive-dev --output table

# View Application Insights logs (last 24 hours)
az monitor app-insights query `
  --app arkive-insights-dev `
  --resource-group rg-arkive-dev `
  --analytics-query "traces | where timestamp > ago(24h) | order by timestamp desc | take 50"

# View Function App deployment status
az functionapp deployment list --name arkive-func-dev --resource-group rg-arkive-dev --output table
```

---

## 14. Redeployment (Updating Code)

After making code changes, here's how to redeploy each component.

### 14a. Redeploy Backend (Azure Functions)

```powershell
# 1. Build the release package
dotnet publish src/Arkive.Functions/Arkive.Functions.csproj `
  --configuration Release `
  --output ./publish/functions

# 2. Create a zip of the published output
Compress-Archive -Path ./publish/functions/* -DestinationPath ./publish/functions.zip -Force

# 3. Deploy to Azure
az functionapp deployment source config-zip `
  --resource-group rg-arkive-dev `
  --name arkive-func-dev `
  --src ./publish/functions.zip

# 4. Verify it's running
curl https://arkive-func-dev.azurewebsites.net/api/health
```

**If you added new database migrations**, run them before deploying the backend:

```powershell
dotnet ef database update `
  --project src/Arkive.Data/Arkive.Data.csproj `
  --startup-project src/Arkive.Functions/Arkive.Functions.csproj `
  --connection "Server=tcp:arkive-sql-dev.database.windows.net,1433;Database=arkive-db-dev;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;"
```

### 14b. Redeploy Frontend (Static Web App)

```powershell
# 1. Build the production frontend
cd src/arkive-web
npm install
npm run build

# 2. Get the deployment token (if you don't have it saved)
az staticwebapp secrets list `
  --name arkive-web-dev `
  --resource-group rg-arkive-dev `
  --query properties.apiKey --output tsv

# 3. Deploy
swa deploy `
  --app-location ./src/arkive-web `
  --output-location .next `
  --deployment-token "YOUR-DEPLOYMENT-TOKEN"
```

**Alternatively**, if you've connected your GitHub repo (Step 8d), just push to `main` and the Static Web App will automatically rebuild and deploy.

### 14c. Redeploy Infrastructure (Bicep)

If you modify the Bicep templates (e.g., adding new resources, changing SKUs):

```powershell
az deployment group create `
  --resource-group rg-arkive-dev `
  --name arkive-deploy `
  --template-file infra/main.bicep `
  --parameters infra/parameters/dev.bicepparam
```

Bicep deployments are idempotent — unchanged resources are skipped, modified resources are updated.

---

## Cost Estimate (Dev Environment)

All resources use consumption/serverless tiers to minimize cost:

| Resource | Tier | Estimated Monthly Cost |
|----------|------|----------------------|
| Azure SQL | Serverless Gen5 (auto-pause after 1hr) | $5-15 (pauses when idle) |
| Azure Functions | Consumption (Y1) | $0-5 (pay per execution) |
| Static Web App | Standard | $9/month |
| Storage Account | Standard LRS | $1-5 |
| Service Bus | Basic | $0.05/month |
| Key Vault | Standard | $0-3 |
| Application Insights | Pay-as-you-go | $0-5 |
| **Total** | | **~$15-40/month** |

The SQL database auto-pauses after 60 minutes of inactivity, which significantly reduces costs during development.

---

## Next Steps After Deployment

1. **Connect your first tenant** -- Use the Arkive UI to connect an M365 tenant via OAuth admin consent
2. **Configure scanning** -- Select SharePoint sites and trigger an initial scan
3. **Set up CI/CD** -- The GitHub Actions workflows (`deploy-staging.yml`, `deploy-production.yml`) are ready for implementation
4. **Configure Teams Bot** -- Register a Bot in Azure Bot Service for the Teams approval workflow (Epic 5)
5. **Set up monitoring alerts** -- Configure Application Insights alerts for errors and performance
