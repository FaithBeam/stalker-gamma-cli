SET script_dir=%~dp0
SET version=%~1
SET choco_api_key=%~2
SET win_x64_sha256=%~3
SET win_arm64_sha256=%~4
SET win_x64_server_sha256=%~5
SET win_arm64_server_sha256=%~6
SET dry_run_flag=
if /I "%~7"=="true" SET dry_run_flag=-DryRun
if not defined version SET version=1.0.0
pwsh.exe -noprofile -executionpolicy bypass -file %script_dir%chocolatey.ps1 -Version %version% -ChocolateyApiKey %choco_api_key% -WinX64Sha256 %win_x64_sha256% -WinArm64Sha256 %win_arm64_sha256% -WinX64ServerSha256 %win_x64_server_sha256% -WinArm64ServerSha256 %win_arm64_server_sha256% %dry_run_flag%
