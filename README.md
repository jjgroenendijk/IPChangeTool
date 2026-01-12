# IPCT (IP Change Tool)

A secure Windows utility that allows standard (non-admin) users to change IP addresses, Subnet Masks, Gateways, and DNS settings of network adapters.

## Overview

Changing network settings normally requires Administrator privileges. This tool solves that by splitting the application into two parts:
1.  **Background Service**: Runs as `LocalSystem` (high privilege) and handles the actual WMI calls to change network settings.
2.  **User Interface**: Runs as the logged-in user (standard privilege) and sends requests to the service via a secure Named Pipe.

## Architecture

### Projects

- **`src/IpChanger.Common`**
    - Shared library containing the data models (`IpConfigRequest`, `IpConfigResponse`) used for communication between the UI and Service.

- **`src/IpChanger.Service`**
    - A Windows Service (`Microsoft.Extensions.Hosting.WindowsServices`) that runs in the background.
    - Listens on a Named Pipe (`IpChangerPipe`) secured to allow Read/Write access to Authenticated Users.
    - Uses WMI (`Win32_NetworkAdapterConfiguration`) to apply IP configurations.
    - Logs activities to the Windows Event Log.

- **`src/IpChanger.UI`**
    - A Windows Forms application for user interaction.
    - Lists available Ethernet and Wireless adapters.
    - Provides a form to set Static IP or enable DHCP.
    - Connects to the Service's Named Pipe to send configuration commands.
    - Includes a status indicator to show connectivity with the background service.

- **`installer/IpChanger.Installer`**
    - A WiX Toolset v4 project.
    - Packages the Service and UI into a single `.msi` installer.
    - Handles the installation and starting of the Windows Service.

## Prerequisites

- **Operating System**: Windows 10/11 or Server (x64).
- **Build Tools**:
    - .NET 8.0 SDK
    - WiX Toolset v4 (Install via: `dotnet tool install --global wix`)

## Building the Project

The project includes a PowerShell script to automate the build process.

1.  Open PowerShell in the solution root.
2.  Run the build script:
    ```powershell
    .\build.ps1
    ```
3.  The script performs the following:
    - Publishes `IpChanger.Service` as a self-contained, single-file executable.
    - Publishes `IpChanger.UI` as a self-contained, single-file executable.
    - Builds the MSI installer.
4.  The final installer is located at:
    `installer\bin\x64\Release\IPCTInstaller.msi`

## Installation & Usage

1.  **Install**: Run the `IPCTInstaller.msi`. You will need Administrator privileges *once* to install the service.
2.  **Run**: Launch **"IPCT"** from the Start Menu.
3.  **Configure**:
    - Select a Network Adapter from the dropdown.
    - Choose **"Obtain IP Automatically (DHCP)"** or enter **Static IP** details.
    - Click **"Apply Settings"**.
4.  **Result**: The application will communicate with the service to apply the changes and report the result.

## Development / Debugging

To test without installing the MSI:

1.  **Run the Service**:
    - Open a terminal as **Administrator**.
    - Navigate to `src/IpChanger.Service`.
    - Run: `dotnet run`
    - The service will start and listen for pipe connections (logs will appear in console).

2.  **Run the UI**:
    - Open a generic terminal (User permissions).
    - Navigate to `src/IpChanger.UI`.
    - Run: `dotnet run`
    - The UI will launch and should connect to the running service instance.

## Author

Developed by [jjgroenendijk.nl](https://jjgroenendijk.nl)
