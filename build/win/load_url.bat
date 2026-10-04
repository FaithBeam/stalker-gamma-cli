SET script_dir=%~dp0
SET arch=%~1
if not defined arch SET arch=x64
powershell.exe -noprofile -executionpolicy bypass -file %script_dir%load_url.ps1 -Arch %arch%
