SET script_dir=%~dp0
SET version=%~1
SET choco_api_key=%~2
if not defined version SET version=1.0.0
if not defined choco_api_key SET choco_api_key=''
powershell.exe -noprofile -executionpolicy bypass -file %script_dir%build.ps1 -Version %version% -ChocolateyApiKey %choco_api_key%
