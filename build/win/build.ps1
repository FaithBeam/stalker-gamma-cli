param (
    [string]$Version = "1.0.0",
    [string]$ChocolateyApiKey
)

Import-Module Microsoft.PowerShell.Utility -Function Get-FileHash
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$PSVersionTable

$scriptDir = $PSScriptRoot

$repoRoot = Resolve-Path $scriptDir\..\..
$buildDir = Join-Path $scriptDir "build"

if (Test-Path $buildDir) {
    Remove-Item -Path $buildDir -Force -Recurse
}

New-Item -Path $buildDir -ItemType Directory -Force

#region 7z
$7zDir = Join-Path $buildDir "7z"
$7zDlPath = Join-Path $7zDir "7z25.01-zstd-x64.exe"
New-Item -Path $7zDir -ItemType Directory -Force
$7zDlSplat = @{
    Uri     = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v25.01-v1.5.7-R3/7z25.01-zstd-x64.exe"
    OutFile = $7zDlPath
}
Invoke-WebRequest @7zDlSplat
tar -xzf $7zDlPath -C $7zDir
#endregion

#region curl-impersonate
$curlDir = Join-Path $buildDir "curl-impersonate"
$curlVersion = "v1.4.4"
$curlArchivePath = Join-Path $curlDir "libcurl-impersonate-$($curlVersion).x86_64-win32.tar.gz"
New-Item -Path "$curlDir" -Type Directory -Force
$curlImpersonateSplat = @{
    Uri     = "https://github.com/lexiforest/curl-impersonate/releases/download/$($curlVersion)/libcurl-impersonate-$($curlVersion).x86_64-win32.tar.gz"
    OutFile = $curlArchivePath
}
Invoke-WebRequest @curlImpersonateSplat
tar -xzf $curlArchivePath -C $curlDir
$cacertSplat = @{
    Uri     = "https://curl.se/ca/cacert.pem"
    OutFile = Join-Path $curlDir "cacert.pem"
}
Invoke-WebRequest @cacertSplat
#endregion

#region dotnet-install
if (-not (Get-Command dotnet)) {
    $dotnetInstallPath = Join-Path $buildDir "dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallPath

    # Install the SDK version required (targeting net10.0 as per project info)
    & $dotnetInstallPath -Channel 10.0 -InstallDir (Join-Path $buildDir ".dotnet")

    # Add the local dotnet to the current session path
    $env:PATH = "$(Join-Path $buildDir ".dotnet");$env:PATH"
    $env:DOTNET_ROOT = "$(Join-Path $buildDir ".dotnet")"
    #endregion
}

#region stalker-gamma-cli
$stalkerCliDir = Join-Path $buildDir "stalker-gamma-cli"
$pathToProject = (Join-Path (Join-Path $repoRoot "stalker-gamma-cli") "stalker-gamma-cli.csproj")
dotnet publish -c Release $pathToProject -o $stalkerCliDir -p:AssemblyVersion=$Version
#endregion

$stalkerCliResourceDir = Join-Path $stalkerCliDir "resources"
New-Item -Path $stalkerCliResourceDir -ItemType Directory -Force

Copy-Item -Path (Join-Path $7zDir "7z.exe") -Destination (Join-Path $stalkerCliResourceDir "7zz.exe")
Copy-Item -Path (Join-Path $7zDir "7z.dll") -Destination (Join-Path $stalkerCliResourceDir "7z.dll")
Copy-Item -Path (Join-Path (Join-Path $curlDir "bin") "curl.exe") -Destination (Join-Path $stalkerCliResourceDir "curl.exe")
Copy-Item -Path (Join-Path $curlDir "cacert.pem") -Destination (Join-Path $stalkerCliResourceDir "cacert.pem")

Remove-Item -Path (Join-Path $stalkerCliDir "*.pdb")

if (Test-Path stalker-gamma+win.x64.zip) {
    Remove-Item stalker-gamma+win.x64.zip -Force
}
& (Join-Path $7zDir "7z.exe") a -tzip -mx9 -r stalker-gamma+win.x64.zip (Join-Path $stalkerCliDir "*")

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

    if (-not ([string]::IsNullOrWhiteSpace($ChocolateyApiKey))) {
        choco push "stalker-gamma.$($Version).nupkg" --source https://push.chocolatey.org/ --api-key $ChocolateyApiKey
    }
}
#endregion