$ErrorActionPreference = "Stop"

Write-Host "Building IpChanger Solution..."

# 1. Publish Service (Single File, Self Contained)
Write-Host "Publishing Service..."
dotnet publish src/IpChanger.Service/IpChanger.Service.csproj -c Release -r win-x64 --self-contained /p:PublishSingleFile=true

# 2. Publish UI (Single File, Self Contained)
Write-Host "Publishing UI..."
dotnet publish src/IpChanger.UI/IpChanger.UI.csproj -c Release -r win-x64 --self-contained /p:PublishSingleFile=true

# 3. Build Installer
# Ensure WiX is installed: dotnet tool install --global wix
Write-Host "Building MSI..."
dotnet build installer/IpChanger.Installer.wixproj -c Release

# 4. Set permissions on MSI file to allow all users to read
Write-Host "Setting file permissions..."
icacls "installer\bin\x64\Release\IpChangerInstaller.msi" /grant Users:R

Write-Host "Done. MSI created at: installer/bin/x64/Release/IpChangerInstaller.msi"
