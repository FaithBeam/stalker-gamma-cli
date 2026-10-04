# Builds load_url (python-cli) and bundles Chrome for Testing and chromedriver
# with it. Output: build-load_url\load_url-bundle
param (
    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64",
    # Latest stable version: https://googlechromelabs.github.io/chrome-for-testing/LATEST_RELEASE_STABLE
    [string]$ChromeVersion = "154.0.8037.92"
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir = $PSScriptRoot

$repoRoot = Resolve-Path $scriptDir\..\..
# Kept apart from build.ps1's build dir so the two never collide.
$buildDir = Join-Path $scriptDir "build-load_url"
$bundleDir = Join-Path $buildDir "load_url-bundle"

if (Test-Path $buildDir) {
    Remove-Item -Path $buildDir -Force -Recurse
}

New-Item -Path $buildDir -ItemType Directory -Force

#region chrome
# Chrome for Testing: Google's portable (zip, no installer) Chrome builds.
# There's no Windows arm64 build, so arm64 uses win64, which Windows 11 on
# arm64 runs under emulation.
$chromeDir = Join-Path $buildDir "chrome"
$chromePlatform = "win64"
New-Item -Path $chromeDir -ItemType Directory -Force
foreach ($name in "chrome", "chromedriver") {
    $archivePath = Join-Path $chromeDir "$name-$chromePlatform.zip"
    $chromeSplat = @{
        Uri     = "https://storage.googleapis.com/chrome-for-testing-public/$ChromeVersion/$chromePlatform/$name-$chromePlatform.zip"
        OutFile = $archivePath
    }
    Invoke-WebRequest @chromeSplat
    Expand-Archive -Path $archivePath -DestinationPath $chromeDir
}
#endregion

#region python-cli
$pythonCliVenvDir = Join-Path $buildDir "python-cli-venv"
$pythonCliDistDir = Join-Path $buildDir "python-cli"
$pythonCliRequirements = Join-Path $repoRoot "python-cli\requirements.txt"
$pythonCliRequirementsDev = Join-Path $repoRoot "python-cli\requirements-dev.txt"
python -m venv $pythonCliVenvDir
& (Join-Path $pythonCliVenvDir "Scripts\pip.exe") install -r $pythonCliRequirements
& (Join-Path $pythonCliVenvDir "Scripts\pip.exe") install -r $pythonCliRequirementsDev
New-Item -Path $pythonCliDistDir -ItemType Directory -Force
& (Join-Path $pythonCliVenvDir "Scripts\pyinstaller.exe") (Join-Path $repoRoot "python-cli\main.py") --onedir --name=load_url --collect-all=seleniumbase --noconfirm --clean --distpath $pythonCliDistDir
#endregion

#region bundle
Copy-Item -Path (Join-Path $pythonCliDistDir "load_url") -Destination $bundleDir -Recurse
# load_url looks for chrome\chrome.exe next to itself
Copy-Item -Path (Join-Path $chromeDir "chrome-$chromePlatform") -Destination (Join-Path $bundleDir "chrome") -Recurse
# load_url points SeleniumBase at chromedriver\
$bundleDriverDir = Join-Path $bundleDir "chromedriver"
New-Item -Path $bundleDriverDir -ItemType Directory -Force
Copy-Item -Path (Join-Path $chromeDir "chromedriver-$chromePlatform\chromedriver.exe") -Destination $bundleDriverDir
#endregion
