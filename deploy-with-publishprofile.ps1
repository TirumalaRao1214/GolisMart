# =============================================================================
# GOLIS Mart — Automated Zip Deploy using Azure Publish Profile / Credentials
# =============================================================================

param (
    [string]$ApiPublishProfilePath = "",
    [string]$WebPublishProfilePath = "",
    [string]$ApiUserName = "",
    [string]$ApiPassword = "",
    [string]$WebUserName = "",
    [string]$WebPassword = ""
)

$ErrorActionPreference = "Stop"

function Deploy-ZipPackage {
    param (
        [string]$AppName,
        [string]$ScmHost,
        [string]$ZipFilePath,
        [string]$UserName,
        [string]$Password,
        [string]$PublishProfilePath
    )

    if ($PublishProfilePath -ne "" -and (Test-Path $PublishProfilePath)) {
        Write-Host "Extracting credentials from publish profile: $PublishProfilePath" -ForegroundColor Cyan
        [xml]$xml = Get-Content $PublishProfilePath
        $profile = $xml.publishData.publishProfile | Where-Object { $_.publishMethod -eq "MSDeploy" -or $_.publishMethod -eq "ZipDeploy" } | Select-Object -First 1
        if (-not $profile) {
            $profile = $xml.publishData.publishProfile[0]
        }
        $UserName = $profile.userName
        $Password = $profile.userPWD
        $ScmHost = $profile.publishUrl.Split(':')[0]
    }

    if (-not $UserName -or -not $Password) {
        Write-Host "Skipping $AppName: No deployment credentials or publish profile provided." -ForegroundColor Yellow
        return
    }

    $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $UserName, $Password)))
    $headers = @{
        "Authorization" = ("Basic {0}" -f $base64Auth)
        "Content-Type"  = "application/zip"
    }

    $deployUri = "https://$ScmHost/api/zipdeploy"
    Write-Host "Deploying $ZipFilePath to $deployUri ..." -ForegroundColor Cyan

    $response = Invoke-RestMethod -Uri $deployUri -Headers $headers -Method Post -InFile $ZipFilePath -TimeoutSec 300
    Write-Host "Successfully deployed $AppName!" -ForegroundColor Green
}

# 1. Build and package latest release
Write-Host "=== Packaging latest release for deployment ===" -ForegroundColor Cyan
powershell -ExecutionPolicy Bypass -File ./publish-to-azure.ps1

# 2. Deploy API
Write-Host "`n=== Deploying API ===" -ForegroundColor Cyan
Deploy-ZipPackage `
    -AppName "golismart-api" `
    -ScmHost "golismart-api-b2gtdwe4e2eugegz.scm.centralindia-01.azurewebsites.net" `
    -ZipFilePath "./publish/api-deploy.zip" `
    -UserName $ApiUserName `
    -Password $ApiPassword `
    -PublishProfilePath $ApiPublishProfilePath

# 3. Deploy Web
Write-Host "`n=== Deploying Web ===" -ForegroundColor Cyan
Deploy-ZipPackage `
    -AppName "golismart-web" `
    -ScmHost "golismart-web-a9gkajajcgbydhha.scm.centralindia-01.azurewebsites.net" `
    -ZipFilePath "./publish/web-deploy.zip" `
    -UserName $WebUserName `
    -Password $WebPassword `
    -PublishProfilePath $WebPublishProfilePath

Write-Host "`n=== Deployment Process Completed ===" -ForegroundColor Green
