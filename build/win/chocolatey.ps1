param (
    [string]$Version = "1.0.0",
    [string]$ChocolateyApiKey,
    [Parameter(Mandatory)]
    [string]$WinX64Sha256,
    [Parameter(Mandatory)]
    [string]$WinArm64Sha256,
    [Parameter(Mandatory)]
    [string]$WinX64ServerSha256,
    [Parameter(Mandatory)]
    [string]$WinArm64ServerSha256
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir = $PSScriptRoot

function New-ChocolateyPackage {
    param (
        [Parameter(Mandatory)]
        [string]$PackageId,
        [Parameter(Mandatory)]
        [string]$ExeName,
        [Parameter(Mandatory)]
        [string]$Description,
        [Parameter(Mandatory)]
        [string]$Summary,
        [Parameter(Mandatory)]
        [string]$WinX64AssetName,
        [Parameter(Mandatory)]
        [string]$WinArm64AssetName,
        [Parameter(Mandatory)]
        [string]$X64Sha256,
        [Parameter(Mandatory)]
        [string]$Arm64Sha256
    )

    $chocoNuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>$($PackageId)</id>
    <version>$($Version)</version>
    <title>$($PackageId)</title>
    <authors>FaithBeam</authors>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <projectUrl>https://github.com/FaithBeam/stalker-gamma-cli</projectUrl>
    <description>$($Description)</description>
    <summary>$($Summary)</summary>
    <tags>stalker-gamma</tags>
  </metadata>
</package>
"@
    $chocoInstall = @"
`$packageName = '$($PackageId)'

if (`$env:PROCESSOR_ARCHITECTURE -eq 'ARM64') {
    `$url      = 'https://github.com/FaithBeam/stalker-gamma-cli/releases/download/$($Version)/$($WinArm64AssetName)'
    `$checksum = '$($Arm64Sha256)'
} else {
    `$url      = 'https://github.com/FaithBeam/stalker-gamma-cli/releases/download/$($Version)/$($WinX64AssetName)'
    `$checksum = '$($X64Sha256)'
}

`$packageArgs = @{
  packageName   = `$packageName
  unzipLocation = "`$(Split-Path -Parent `$MyInvocation.MyCommand.Definition)"
  url           = `$url
  checksum      = `$checksum
  checksumType  = 'sha256'
}

`$toolsDir = "`$(Split-Path -Parent `$MyInvocation.MyCommand.Definition)"
Install-ChocolateyZipPackage @packageArgs

`$filesToIgnore = Get-ChildItem "`$toolsDir\*.exe" -Recurse | Where-Object { `$_.Name -ne "$($ExeName)" }

foreach (`$file in `$filesToIgnore) {
    New-Item "`$(`$file.FullName).ignore" -Type File -Force | Out-Null
}
"@

    $chocolateyDir = Join-Path (Join-Path $scriptDir "chocolatey") $PackageId
    $chocolateyToolsDir = Join-Path $chocolateyDir "tools"
    $chocolateyNuspecPath = Join-Path $chocolateyDir "$($PackageId).nuspec"
    $chocolateyInstallPath = Join-Path $chocolateyToolsDir "chocolateyinstall.ps1"
    if (Test-Path $chocolateyDir) {
        Remove-Item $chocolateyDir -Force -Recurse
    }

    New-Item $chocolateyDir -ItemType Directory -Force
    New-Item $chocolateyToolsDir -ItemType Directory -Force
    Out-File $chocolateyNuspecPath -InputObject $chocoNuspec -Encoding utf8
    Out-File $chocolateyInstallPath -InputObject $chocoInstall -Encoding utf8

    choco pack $chocolateyNuspecPath --outputdirectory $chocolateyDir

    choco push (Join-Path $chocolateyDir "$($PackageId).$($Version).nupkg") --source https://push.chocolatey.org/
}

#region chocolatey
if (Get-Command choco) {
    choco apikey --key $ChocolateyApiKey --source https://push.chocolatey.org/

    New-ChocolateyPackage `
        -PackageId "stalker-gamma" `
        -ExeName "stalker-gamma.exe" `
        -Description "stalker-gamma-cli is a cli to install Stalker Anomaly and the GAMMA mod pack." `
        -Summary "Install Stalker GAMMA via cli" `
        -WinX64AssetName "stalker-gamma+win.x64.zip" `
        -WinArm64AssetName "stalker-gamma+win.arm64.zip" `
        -X64Sha256 $WinX64Sha256 `
        -Arm64Sha256 $WinArm64Sha256

    New-ChocolateyPackage `
        -PackageId "stalker-gamma-server" `
        -ExeName "stalker-gamma-server.exe" `
        -Description "stalker-gamma-server is the companion server for stalker-gamma-cli." `
        -Summary "Companion server for stalker-gamma-cli" `
        -WinX64AssetName "stalker-gamma-server+win.x64.zip" `
        -WinArm64AssetName "stalker-gamma-server+win.arm64.zip" `
        -X64Sha256 $WinX64ServerSha256 `
        -Arm64Sha256 $WinArm64ServerSha256
} else {
    throw "choco not found in PATH"
}
#endregion
