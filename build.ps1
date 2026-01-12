param (
    [string]$Arch = "x64",
    [string]$BuildDate = ""
)

$ErrorActionPreference = "Stop"
$Runtime = "win-$Arch"

# If no BuildDate provided, use current date
if ([string]::IsNullOrEmpty($BuildDate)) {
    $BuildDate = (Get-Date).ToString("yyyy-MM-dd")
}

Write-Host "Building IPCT Solution for $Arch (Build: $BuildDate)..."

# 1. Publish Service (Single File, Self Contained)
Write-Host "Publishing Service..."
dotnet publish src/IpChanger.Service/IpChanger.Service.csproj -c Release -r $Runtime --self-contained /p:PublishSingleFile=true

# 2. Publish UI (Single File, Self Contained)
Write-Host "Publishing UI..."
dotnet publish src/IpChanger.UI/IpChanger.UI.csproj -c Release -r $Runtime --self-contained /p:PublishSingleFile=true /p:SourceRevisionId=$BuildDate

# Define paths for WiX (needs to be passed to the project)
# Use absolute paths to avoid confusion
$ServiceDir = Convert-Path "src\IpChanger.Service\bin\Release\net8.0\$Runtime\publish"
$UIDir = Convert-Path "src\IpChanger.UI\bin\Release\net8.0-windows\$Runtime\publish"

# 3. Build Installer
# Ensure WiX is installed: dotnet tool install --global wix
Write-Host "Building MSI for $Arch..."
# Note: Output path needs to be specific per arch if running in parallel or sequentially to avoid overwrite, 
# but for now we let it build to default bin/Arch/Release
dotnet build installer/IpChanger.Installer.wixproj -c Release /p:Platform=$Arch /p:ServiceSourceDir="$ServiceDir" /p:UISourceDir="$UIDir"

# 4. Set permissions on MSI file to allow all users to read
# The output path depends on the platform.
$MsiPath = "installer\bin\$Arch\Release\IPCTInstaller.msi"

Write-Host "Setting file permissions on $MsiPath..."
if (Test-Path $MsiPath) {
    icacls $MsiPath /grant Users:R
    Write-Host "Done. MSI created at: $MsiPath"
} else {
    Write-Error "MSI not found at expected path: $MsiPath"
}
