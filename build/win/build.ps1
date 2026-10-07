param (
    [string]$Version = "1.0.0",
    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64"
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir = $PSScriptRoot

$repoRoot = Resolve-Path $scriptDir\..\..
$buildDir = Join-Path $scriptDir "build"

if (Test-Path $buildDir) {
    Remove-Item -Path $buildDir -Force -Recurse
}

New-Item -Path $buildDir -ItemType Directory -Force

#region 7z
$7zDir = Join-Path $buildDir "7z"
New-Item -Path $7zDir -ItemType Directory -Force

$7zFileName = "7z2603-$Arch.exe"
$7zDlPath = Join-Path $7zDir $7zFileName
$7zDlSplat = @{
    Uri     = "https://github.com/ip7z/7zip/releases/download/26.03/$7zFileName"
    OutFile = $7zDlPath
}
Invoke-WebRequest @7zDlSplat
# force the self-extracting 7zip exe to run as a user, bypassing UAC
$env:__COMPAT_LAYER="RunAsInvoker"
& $7zDlPath /S /D="$($7zDir)"
$env:__COMPAT_LAYER=""
#endregion

#region curl-impersonate
$curlDir = Join-Path $buildDir "curl-impersonate"
$curlVersion = "v2.2.2"
$curlTriplet = if ($Arch -eq "arm64") { "arm64-win32" } else { "x86_64-win32" }
$curlArchiveName = "libcurl-impersonate-$($curlVersion).$curlTriplet.tar.gz"
$curlArchivePath = Join-Path $curlDir $curlArchiveName
New-Item -Path "$curlDir" -Type Directory -Force
$curlImpersonateSplat = @{
    Uri     = "https://github.com/lexiforest/curl-impersonate/releases/download/$($curlVersion)/$curlArchiveName"
    OutFile = $curlArchivePath
}
Invoke-WebRequest @curlImpersonateSplat
# 7z unpacks .tar.gz in two steps: .gz -> .tar, then .tar -> files
$7zExe = Join-Path $7zDir "7z.exe"
& $7zExe x $curlArchivePath "-o$curlDir" -y
$curlTarPath = Join-Path $curlDir ([System.IO.Path]::GetFileNameWithoutExtension($curlArchiveName))
& $7zExe x $curlTarPath "-o$curlDir" -y
Remove-Item $curlTarPath
$cacertSplat = @{
    Uri     = "https://curl.se/ca/cacert.pem"
    OutFile = Join-Path $curlDir "cacert.pem"
}
Invoke-WebRequest @cacertSplat
#endregion

#region stalker-gamma-cli
$stalkerCliDir = Join-Path $buildDir "stalker-gamma-cli"
$pathToProject = (Join-Path (Join-Path $repoRoot "stalker-gamma-cli") "stalker-gamma-cli.csproj")
$dotnetRid = "win-$Arch"
dotnet publish -c Release $pathToProject -o $stalkerCliDir -r $dotnetRid -p:AssemblyVersion=$Version
#endregion

$stalkerCliResourceDir = Join-Path $stalkerCliDir "resources"
New-Item -Path $stalkerCliResourceDir -ItemType Directory -Force

Copy-Item -Path (Join-Path $7zDir "7z.exe") -Destination (Join-Path $stalkerCliResourceDir "7zz.exe")
Copy-Item -Path (Join-Path $7zDir "7z.dll") -Destination (Join-Path $stalkerCliResourceDir "7z.dll")
Move-Item (Join-Path (Join-Path $curlDir "lib") "libcurl-impersonate.dll") $stalkerCliDir
Copy-Item -Path (Join-Path $curlDir "cacert.pem") -Destination (Join-Path $stalkerCliDir "cacert.pem")

Remove-Item -Path (Join-Path $stalkerCliDir "*.pdb")

#region stalker-gamma-server
$stalkerCliServerDir = Join-Path $buildDir "stalker-gamma-server"
$pathToServerProject = (Join-Path (Join-Path $repoRoot "stalker-gamma-cli-server") "stalker-gamma-cli-server.csproj")
dotnet publish -c Release $pathToServerProject -o $stalkerCliServerDir -r $dotnetRid -p:AssemblyVersion=$Version
Remove-Item -Path (Join-Path $stalkerCliServerDir "*.pdb") -ErrorAction SilentlyContinue
#endregion

$zipName = "stalker-gamma+win.$Arch.zip"
if (Test-Path $zipName) {
    Remove-Item $zipName -Force
}
& (Join-Path $7zDir "7z.exe") a -tzip -mx9 -r $zipName (Join-Path $stalkerCliDir "*")

$serverZipName = "stalker-gamma-server+win.$Arch.zip"
if (Test-Path $serverZipName) {
    Remove-Item $serverZipName -Force
}
& (Join-Path $7zDir "7z.exe") a -tzip -mx9 -r $serverZipName (Join-Path $stalkerCliServerDir "*")
