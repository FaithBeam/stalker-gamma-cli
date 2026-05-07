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

# Architecture-specific mappings
$archConfig = @{
    "x64" = @{
        SevenZipUrl    = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v25.01-v1.5.7-R3/7z25.01-zstd-x64.exe"
        SevenZipExe    = "7z25.01-zstd-x64.exe"
        DotnetArch     = "x64"
        DotnetRid      = "win-x64"
        OutputZip      = "stalker-gamma+win.x64.zip"
    }
    "arm64" = @{
        SevenZipUrl    = "https://github.com/mcmilk/7-Zip-zstd/releases/download/v25.01-v1.5.7-R3/7z25.01-zstd-arm64.exe"
        SevenZipExe    = "7z25.01-zstd-arm64.exe"
        DotnetArch     = "arm64"
        DotnetRid      = "win-arm64"
        OutputZip      = "stalker-gamma+win.arm64.zip"
    }
}

$cfg = $archConfig[$Arch]

if (Test-Path $buildDir) {
    Remove-Item -Path $buildDir -Force -Recurse
}

New-Item -Path $buildDir -ItemType Directory -Force

#region 7z
$7zDir = Join-Path $buildDir "7z"
$7zDlPath = Join-Path $7zDir $cfg.SevenZipExe
New-Item -Path $7zDir -ItemType Directory -Force
$7zDlSplat = @{
    Uri     = $cfg.SevenZipUrl
    OutFile = $7zDlPath
}
Invoke-WebRequest @7zDlSplat
tar -xzf $7zDlPath -C $7zDir
#endregion

#region dotnet-install
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $dotnetInstallPath = Join-Path $buildDir "dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallPath

    & $dotnetInstallPath -Channel 10.0 -InstallDir (Join-Path $buildDir ".dotnet") -Architecture $cfg.DotnetArch

    $env:PATH = "$(Join-Path $buildDir ".dotnet");$env:PATH"
    $env:DOTNET_ROOT = "$(Join-Path $buildDir ".dotnet")"
}
#endregion

#region stalker-gamma-cli
$stalkerCliDir = Join-Path $buildDir "stalker-gamma-cli"
$pathToProject = (Join-Path (Join-Path $repoRoot "stalker-gamma-cli") "stalker-gamma-cli.csproj")
dotnet publish -c Release $pathToProject -o $stalkerCliDir -r $cfg.DotnetRid -p:AssemblyVersion=$Version
#endregion

#region python-api
$pyinstallerDistDir = Join-Path $buildDir "pyinstaller-dist"
$pythonApiDir = Join-Path $repoRoot "python-api"
pyinstaller --distpath $pyinstallerDistDir (Join-Path $pythonApiDir "main.spec")
#endregion

$stalkerCliResourceDir = Join-Path $stalkerCliDir "resources"
New-Item -Path $stalkerCliResourceDir -ItemType Directory -Force

Copy-Item -Path (Join-Path $7zDir "7z.exe") -Destination (Join-Path $stalkerCliResourceDir "7zz.exe")
Copy-Item -Path (Join-Path $7zDir "7z.dll") -Destination (Join-Path $stalkerCliResourceDir "7z.dll")
Copy-Item -Path (Join-Path $pyinstallerDistDir "cloudscraper.exe") -Destination (Join-Path $stalkerCliResourceDir "cloudscraper.exe")

Remove-Item -Path (Join-Path $stalkerCliDir "*.pdb")

if (Test-Path $cfg.OutputZip) {
    Remove-Item $cfg.OutputZip -Force
}
& (Join-Path $7zDir "7z.exe") a -tzip -mx9 -r $cfg.OutputZip (Join-Path $stalkerCliDir "*")