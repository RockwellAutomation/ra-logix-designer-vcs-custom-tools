<#
.SYNOPSIS
    Copies the built CustomToolsMenu.xml to the Logix Designer common directory.

.DESCRIPTION
    This script copies artifacts\bin\Release\Assets\CustomToolsMenu.xml to
    C:\Program Files (x86)\Rockwell Software\RSLogix 5000\Common\CustomToolsMenu.xml
    so that the custom tools are available globally within Logix Designer.

    The script will self-elevate to Administrator if not already running elevated,
    because the destination is under Program Files.

.PARAMETER Configuration
    Build configuration to copy from. Defaults to 'Release'.
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$source      = Join-Path $PSScriptRoot "artifacts\bin\$Configuration\Assets\CustomToolsMenu.xml"
$destination = 'C:\Program Files (x86)\Rockwell Software\RSLogix 5000\Common\CustomToolsMenu.xml'

if (-not (Test-Path $source)) {
    Write-Error "Source file not found: $source`nPlease build the solution first: dotnet build -c $Configuration"
    return
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

if (-not $isAdmin) {
    Write-Host 'Requesting Administrator privileges...' -ForegroundColor Yellow
    $psArgs = @(
        '-NoProfile'
        '-ExecutionPolicy', 'Bypass'
        '-File', "`"$PSCommandPath`""
        '-Configuration', $Configuration
    )
    Start-Process pwsh -ArgumentList $psArgs -Verb RunAs -Wait
    return
}

$destDir = Split-Path $destination -Parent
if (-not (Test-Path $destDir)) {
    Write-Error "Logix Designer common directory not found: $destDir`nIs Logix Designer installed?"
    return
}

# Backup existing file
if (Test-Path $destination) {
    $i = 1
    while (Test-Path "$destination.bak$i") { $i++ }
    $backupPath = "$destination.bak$i"
    Move-Item -Path $destination -Destination $backupPath
    Write-Host "Backed up existing file to:`n  $backupPath" -ForegroundColor Cyan
}

Copy-Item -Path $source -Destination $destination
Write-Host "Copied:`n  $source`n  -> $destination" -ForegroundColor Green
