#!/bin/bash
# deploy-with-chat.sh – Deploy Expense Management with GenAI (Azure OpenAI + AI Search)
# Usage: bash deploy-with-chat.sh
# Prerequisites: az login, dotnet 8 SDK

set -e

# ============================================================
# VARIABLES – update these before running
# ============================================================
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)

DAY=$(date +%d)
MONTH=$(date +%m)
MINUTE=$(date +%M)

echo "============================================================"
echo "Deploying Expense Management System WITH GenAI"
echo "Resource Group : $RESOURCE_GROUP"
echo "Location       : $LOCATION"
echo "Subscription   : $SUBSCRIPTION_ID"
echo "Admin Login    : $ADMIN_LOGIN"
echo "============================================================"

# ============================================================
# 1. Create resource group
# ============================================================
echo ""
echo "Step 1: Creating resource group..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo "✓ Resource group created"

# ============================================================
# 2. Deploy main.bicep WITH GenAI
# ============================================================
echo ""
echo "Step 2: Deploying infrastructure (App Service + SQL + GenAI)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters location="$LOCATION" \
                 adminObjectId="$ADMIN_OBJECT_ID" \
                 adminLogin="$ADMIN_LOGIN" \
                 deployGenAI=true \
                 day="$DAY" \
                 month="$MONTH" \
                 minute="$MINUTE" \
    --query "properties.outputs" \
    --output json)

echo "✓ Infrastructure deployed"

# ============================================================
# 3. Extract outputs
# ============================================================
APP_SERVICE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
SQL_SERVER_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerName.value')
SQL_DATABASE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlDatabaseName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value')

echo ""
echo "Outputs:"
echo "  App Service    : $APP_SERVICE_NAME"
echo "  App URL        : $APP_SERVICE_URL"
echo "  SQL Server     : $SQL_SERVER_FQDN"
echo "  MI Name        : $MANAGED_IDENTITY_NAME"
echo "  MI Client ID   : $MANAGED_IDENTITY_CLIENT_ID"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model   : $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"

# ============================================================
# 4. Configure App Service settings (including GenAI)
# ============================================================
echo ""
echo "Step 4: Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN};Database=${SQL_DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_SERVICE_NAME" \
    --settings \
        "AZURE_CLIENT_ID=${MANAGED_IDENTITY_CLIENT_ID}" \
        "ManagedIdentityClientId=${MANAGED_IDENTITY_CLIENT_ID}" \
        "OpenAI__Endpoint=${OPENAI_ENDPOINT}" \
        "OpenAI__DeploymentName=${OPENAI_MODEL_NAME}" \
        "GenAISettings__Endpoint=${OPENAI_ENDPOINT}" \
        "GenAISettings__DeploymentName=${OPENAI_MODEL_NAME}" \
        "GenAISettings__SearchEndpoint=${SEARCH_ENDPOINT}" \
    --output none

az webapp config connection-string set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_SERVICE_NAME" \
    --connection-string-type SQLAzure \
    --settings "DefaultConnection=${CONNECTION_STRING}" \
    --output none

echo "✓ App Service settings configured (including GenAI endpoints)"

# ============================================================
# 5. Update Python scripts with correct server/database
# ============================================================
echo ""
echo "Step 5: Updating Python scripts with deployment values..."
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${SQL_DATABASE_NAME}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${SQL_DATABASE_NAME}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${SQL_DATABASE_NAME}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
echo "✓ Python scripts updated"

# ============================================================
# 6. Wait for SQL Server to be ready
# ============================================================
echo ""
echo "Step 6: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# ============================================================
# 7. Configure SQL Firewall
# ============================================================
echo ""
echo "Step 7: Configuring SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)

az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowDeploymentIP" \
    --start-ip-address "$MY_IP" \
    --end-ip-address "$MY_IP" \
    --output none

echo "✓ Firewall rules added (Azure services + deployment IP $MY_IP)"
echo "Waiting 15 seconds for firewall rules to propagate..."
sleep 15

# ============================================================
# 8. Install Python dependencies
# ============================================================
echo ""
echo "Step 8: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "✓ Python dependencies installed"

# ============================================================
# 9. Import database schema
# ============================================================
echo ""
echo "Step 9: Importing database schema..."
python3 run-sql.py
echo "✓ Schema imported"

# ============================================================
# 10. Set up managed identity database roles
# ============================================================
echo ""
echo "Step 10: Configuring managed identity database roles..."
sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py
echo "✓ Database roles configured"

# ============================================================
# 11. Create stored procedures
# ============================================================
echo ""
echo "Step 11: Creating stored procedures..."
python3 run-sql-stored-procs.py
echo "✓ Stored procedures created"

# ============================================================
# 12. Build and deploy app code
# ============================================================
echo ""
echo "Step 12: Building and packaging app..."
cd app
dotnet publish -c Release -o ./publish --nologo -q
cd publish
zip -r ../../app.zip . -x "*.pdb"
cd ../..
echo "✓ App packaged as app.zip"

echo ""
echo "Step 13: Deploying app to Azure App Service..."
az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_SERVICE_NAME" \
    --src-path ./app.zip \
    --type zip \
    --output none
echo "✓ App deployed"

# ============================================================
# Summary
# ============================================================
echo ""
echo "============================================================"
echo "✅ DEPLOYMENT WITH GENAI COMPLETE"
echo "============================================================"
echo ""
echo "🌐 App URL      : ${APP_SERVICE_URL}/Index"
echo "🔌 API Docs     : ${APP_SERVICE_URL}/swagger"
echo "💬 Chat UI      : ${APP_SERVICE_URL}/chat"
echo "🤖 OpenAI       : ${OPENAI_ENDPOINT}"
echo "🔍 AI Search    : ${SEARCH_ENDPOINT}"
echo ""
echo "NOTE: Navigate to ${APP_SERVICE_URL}/Index (not the root URL)"
echo ""
