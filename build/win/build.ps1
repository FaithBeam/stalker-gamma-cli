$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

param (
    [string]$Version = "1.0.0"
)

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
    Uri = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v25.01-v1.5.7-R3/7z25.01-zstd-x64.exe"
    OutFile = $7zDlPath
}
Invoke-WebRequest @7zDlSplat
tar -xzf $7zDlPath -C $7zDir
#endregion

#region curl-impersonate
$curlDir = Join-Path $buildDir "curl-impersonate"
$curlArchivePath = Join-Path $curlDir "libcurl-impersonate-v1.2.5.x86_64-win32.tar.gz"
New-Item -Path "$curlDir" -Type Directory -Force
$curlImpersonateSplat = @{
    Uri = "https://github.com/lexiforest/curl-impersonate/releases/download/v1.2.5/libcurl-impersonate-v1.2.5.x86_64-win32.tar.gz"
    OutFile = $curlArchivePath
}
Invoke-WebRequest @curlImpersonateSplat
tar -xzf $curlArchivePath -C $curlDir
$cacertSplat = @{
    Uri = "https://curl.se/ca/cacert.pem"
    OutFile = Join-Path $curlDir "cacert.pem"
}
Invoke-WebRequest @cacertSplat
#endregion

#region git
$gitDir = Join-Path $buildDir "git"
$gitDlPath = Join-Path $gitDir "MinGit-2.52.0-busybox-64-bit.zip"
New-Item -Path $gitDir -ItemType Directory -Force
$gitDlSplat = @{
    Uri = "https://github.com/git-for-windows/git/releases/download/v2.52.0.windows.1/MinGit-2.52.0-busybox-64-bit.zip"
    OutFile = $gitDlPath
}
Invoke-WebRequest @gitDlSplat
$gitExtractDir = Join-Path $gitDir "git"
New-Item -Path $gitExtractDir -ItemType Directory -Force
tar -xzf $gitDlPath -C $gitExtractDir
#endregion

#region dotnet-install
$dotnetInstallPath = Join-Path $buildDir "dotnet-install.ps1"
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallPath

# Install the SDK version required (targeting net10.0 as per project info)
& $dotnetInstallPath -Channel 10.0 -InstallDir (Join-Path $buildDir ".dotnet")

# Add the local dotnet to the current session path
$env:PATH = "$(Join-Path $buildDir ".dotnet");$env:PATH"
$env:DOTNET_ROOT = "$(Join-Path $buildDir ".dotnet")"
#endregion

#region stalker-gamma-cli
$stalkerCliDir = Join-Path $buildDir "stalker-gamma-cli"
$pathToProject = (Join-Path (Join-Path $repoRoot "stalker-gamma-cli") "stalker-gamma-cli.csproj")
dotnet publish -c Release $pathToProject -o $stalkerCliDir -p:AssemblyVersion=$Version
#endregion

$stalkerCliResourceDir = Join-Path $stalkerCliDir "resources"
New-Item -Path $stalkerCliResourceDir -ItemType Directory -Force

Copy-Item -Path $gitExtractDir -Destination (Join-Path $stalkerCliResourceDir "git") -Recurse
Copy-Item -Path (Join-Path $7zDir "7z.exe") -Destination (Join-Path $stalkerCliResourceDir "7zz.exe")
Copy-Item -Path (Join-Path $7zDir "7z.dll") -Destination (Join-Path $stalkerCliResourceDir "7z.dll")
Copy-Item -Path (Join-Path (Join-Path $curlDir "bin") "curl.exe") -Destination (Join-Path $stalkerCliResourceDir "curl.exe")
Copy-Item -Path (Join-Path $curlDir "cacert.pem") -Destination (Join-Path $stalkerCliResourceDir "cacert.pem")

Remove-Item -Path (Join-Path $stalkerCliDir "*.pdb")

& (Join-Path $7zDir "7z.exe") a -r stalker-gamma-cli.7z (Join-Path (Join-Path "." $stalkerCliDir) "*")
