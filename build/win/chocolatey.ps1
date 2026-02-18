param (
    [string]$Version = "1.0.0",
    [string]$ChocolateyApiKey
)

Import-Module $PSHOME\Modules\Microsoft.PowerShell.Utility -Function Get-FileHash
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$archiveSha256 = (Get-FileHash stalker-gamma+win.x64.zip).Hash.ToLower()

#region chocolatey
if (Get-Command choco) {
    $chocoNuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>stalker-gamma</id>
    <version>$($Version)</version>
    <title>stalker-gamma</title>
    <authors>FaithBeam</authors>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <projectUrl>https://github.com/FaithBeam/stalker-gamma-cli</projectUrl>
    <description>stalker-gamma-cli is a cli to install Stalker Anomaly and the GAMMA mod pack.</description>
    <summary>Install Stalker GAMMA via cli</summary>
    <tags>stalker-gamma</tags>
  </metadata>
</package>
"@
    $chocoInstall = @"
`$packageName = 'stalker-gamma'
`$url         = 'https://github.com/FaithBeam/stalker-gamma-cli/releases/download/$($Version)/stalker-gamma+win.x64.zip'
`$checksum    = '$($archiveSha256)'

`$packageArgs = @{
  packageName   = `$packageName
  unzipLocation = "`$(Split-Path -Parent `$MyInvocation.MyCommand.Definition)"
  url           = `$url
  checksum      = `$checksum
  checksumType  = 'sha256'
}

`$toolsDir = "`$(Split-Path -Parent `$MyInvocation.MyCommand.Definition)"
Install-ChocolateyZipPackage @packageArgs

`$filesToIgnore = Get-ChildItem "`$toolsDir\*.exe" -Recurse | Where-Object { `$_.Name -ne "stalker-gamma.exe" }

foreach (`$file in `$filesToIgnore) {
    New-Item "`$(`$file.FullName).ignore" -Type File -Force | Out-Null
}
"@

    $chocolateyDir = Join-Path $scriptDir "chocolatey"
    $chocolateyToolsDir = Join-Path $chocolateyDir "tools"
    $chocolateyNuspecPath = Join-Path $chocolateyDir "stalker-gamma.nuspec"
    $chocolateyInstallPath = Join-Path $chocolateyToolsDir "chocolateyinstall.ps1"
    if (Test-Path $chocolateyDir) {
        Remove-Item $chocolateyDir -Force -Recurse
    }

    New-Item $chocolateyDir -ItemType Directory -Force
    New-Item $chocolateyToolsDir -ItemType Directory -Force
    Out-File $chocolateyNuspecPath -InputObject $chocoNuspec -Encoding utf8
    Out-File $chocolateyInstallPath -InputObject $chocoInstall -Encoding utf8

    choco pack $chocolateyNuspecPath

    choco push "stalker-gamma.$($Version).nupkg" --source https://push.chocolatey.org/ --api-key $ChocolateyApiKey
} else {
    throw "choco not found in PATH"
}
#endregion