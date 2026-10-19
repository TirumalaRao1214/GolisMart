# =============================================================================
# GOLIS Mart — Local Build & Package for Azure App Service Zip Deploy
# =============================================================================

param (
    [string]$OutputDir = "./publish"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building & Publishing GOLIS Mart Solution ===" -ForegroundColor Cyan

# 1. Clean previous publish folder
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}

# 2. Publish API
Write-Host "`n[1/4] Publishing GOLISMart.API (Release)..." -ForegroundColor Yellow
dotnet publish ./GOLISMart.API/GOLISMart.API.csproj -c Release -o "$OutputDir/api"

# 3. Publish Web
Write-Host "`n[2/4] Publishing GOLISMart.Web (Release)..." -ForegroundColor Yellow
dotnet publish ./GOLISMart.Web/GOLISMart.Web.csproj -c Release -o "$OutputDir/web"

# 4. Create Zip packages for Azure App Service Deployment
Write-Host "`n[3/4] Creating ZIP archives for Azure Deployment..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDir/api/*" -DestinationPath "$OutputDir/api-deploy.zip" -Force
Compress-Archive -Path "$OutputDir/web/*" -DestinationPath "$OutputDir/web-deploy.zip" -Force

Write-Host "`n[4/4] Packages ready!" -ForegroundColor Green
Write-Host "--------------------------------------------------"
Write-Host "API Package:  $OutputDir/api-deploy.zip"
Write-Host "Web Package:  $OutputDir/web-deploy.zip"
Write-Host "--------------------------------------------------"
Write-Host "Deploy these ZIP files to your Azure App Services via:"
Write-Host "1. Azure Portal: App Service -> Deployment Center -> FTPS / Zip Push"
Write-Host "2. Azure CLI:   az webapp deploy --resource-group <rg> --name <app> --src-path $OutputDir/api-deploy.zip"
Write-Host "3. Kudu Zip API: https://<app-name>.scm.azurewebsites.net/api/zipdeploy"
