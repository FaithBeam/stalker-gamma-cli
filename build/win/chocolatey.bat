SET script_dir=%~dp0
SET version=%~1
SET choco_api_key=%~2
SET path_to_archive=%~3
if not defined version SET version=1.0.0
powershell.exe -noprofile -executionpolicy bypass -file %script_dir%chocolatey.ps1 -Version %version% -ChocolateyApiKey %choco_api_key% -PathToArchive path_to_archive
