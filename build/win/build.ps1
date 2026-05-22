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

$7zFileName = "7z25.01-zstd-$Arch.exe"
$7zDlPath = Join-Path $7zDir $7zFileName
$7zDlSplat = @{
    Uri     = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v25.01-v1.5.7-R3/$7zFileName"
    OutFile = $7zDlPath
}
Invoke-WebRequest @7zDlSplat
tar -xzf $7zDlPath -C $7zDir
#endregion

#region curl-impersonate
$curlDir = Join-Path $buildDir "curl-impersonate"
$curlVersion = "v1.5.6"
$curlTriplet = if ($Arch -eq "arm64") { "arm64-win32" } else { "x86_64-win32" }
$curlArchiveName = "libcurl-impersonate-$($curlVersion).$curlTriplet.tar.gz"
$curlArchivePath = Join-Path $curlDir $curlArchiveName
New-Item -Path "$curlDir" -Type Directory -Force
$curlImpersonateSplat = @{
    Uri     = "https://github.com/lexiforest/curl-impersonate/releases/download/$($curlVersion)/$curlArchiveName"
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

#region cloudscraper
$cloudscraperVenvDir = Join-Path $buildDir "cloudscraper-venv"
$cloudscraperDistDir = Join-Path $buildDir "cloudscraper"
$cloudscraperSpec = Join-Path $repoRoot "python-api\main.spec"
$cloudscraperRequirements = Join-Path $repoRoot "python-api\requirements.txt"
python -m venv $cloudscraperVenvDir
& (Join-Path $cloudscraperVenvDir "Scripts\pip.exe") install -r $cloudscraperRequirements
New-Item -Path $cloudscraperDistDir -ItemType Directory -Force
& (Join-Path $cloudscraperVenvDir "Scripts\pyinstaller.exe") --distpath $cloudscraperDistDir $cloudscraperSpec
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
Copy-Item -Path (Join-Path $cloudscraperDistDir "cloudscraper.exe") -Destination $stalkerCliResourceDir -Recurse
Move-Item (Join-Path (Join-Path $curlDir "bin") "libcurl-impersonate.dll") $stalkerCliDir
Copy-Item -Path (Join-Path $curlDir "cacert.pem") -Destination (Join-Path $stalkerCliDir "cacert.pem")

Remove-Item -Path (Join-Path $stalkerCliDir "*.pdb")

$zipName = "stalker-gamma+win.$Arch.zip"
if (Test-Path $zipName) {
    Remove-Item $zipName -Force
}
& (Join-Path $7zDir "7z.exe") a -tzip -mx9 -r $zipName (Join-Path $stalkerCliDir "*")
