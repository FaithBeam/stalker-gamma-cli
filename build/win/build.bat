SET script_dir=%~dp0
SET version=%~1
SET arch=%~2
if not defined version SET version=1.0.0
if not defined arch SET arch=x64
powershell.exe -noprofile -executionpolicy bypass -file %script_dir%build.ps1 -Version %version% -Arch %arch%
