# stalker-gamma-cli

_stalker-gamma-cli_ installs and updates Stalker Anomaly and the [G.A.M.M.A.](https://github.com/Grokitach/Stalker_GAMMA) mod pack from the command line on Windows, Linux and macOS.

## Features

- Install and update Stalker Anomaly and GAMMA
- [Resume](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Advanced#resume-a-failed-install) failed installs and updates
- [Offline installs](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Offline-Install) from a cache archive
- [Multiple install profiles](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Advanced#multiple-profiles), e.g. stable and testing
- Manage Mod Organizer 2 profiles and mods
- [Anomaly mirror](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Anomaly-Mirror) that extracts about 9× faster
- [Get past ModDB's Cloudflare challenge](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Stalker-GAMMA-Server) (experimental)
- [Prune outdated addons](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Advanced#freeing-up-cache-space) from your cache
- [Machine-readable JSON output](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Json-Output) for GUI wrappers

## Install

A full install needs about **150 GB** of free space.

| OS      | Install the CLI                                                                                      | Guide                                                                                  |
| ------- | ---------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Windows | `choco install stalker-gamma`                                                                        | [Windows Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Windows-Install) |
| Linux   | `yay -S stalker-gamma-cli-bin` or the AppImage from [Releases](https://github.com/FaithBeam/stalker-gamma-cli/releases) | [Linux Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Linux-Install)     |
| macOS   | `brew tap faithbeam/stalker-gamma && brew install --cask stalker-gamma`                              | [macOS Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/MacOS-Install)     |

The guides cover creating a profile, running `full-install`, and setting up Wine/Proton or a Sikarugir bottle on Linux and macOS.

## Update

After a full install, check for and apply updates:

```bash
stalker-gamma update check
stalker-gamma update apply --preserve-user-settings --preserve-mcm-settings
```

The switches keep your `user.ltx` (hotkeys, graphics) and MCM settings. See [Advanced](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Advanced) for details.

## Documentation

The [wiki](https://github.com/FaithBeam/stalker-gamma-cli/wiki) has everything else:

- **Install:** [Windows](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Windows-Install) · [Linux](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Linux-Install) · [macOS](https://github.com/FaithBeam/stalker-gamma-cli/wiki/MacOS-Install) · [Offline Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Offline-Install) · [Create Offline Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Create-Offline-Install)
- **Usage:** [Commands](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Commands) · [Advanced](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Advanced) · [Anomaly Mirror](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Anomaly-Mirror) · [Stalker GAMMA Server](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Stalker-GAMMA-Server) · [FSR Upscaling](https://github.com/FaithBeam/stalker-gamma-cli/wiki/FSR-Upscaling) · [Steam Deck / Potato Mode](https://github.com/FaithBeam/stalker-gamma-cli/wiki/FPS-Config-%7C-Steam-Deck-%7C-Potato-Mode-Modlist)
- **Developers:** [JSON Output](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Json-Output)

Run `stalker-gamma --help`, or `--help` on any command, to see the commands and options for your installed version.

## Troubleshooting

See [Troubleshooting](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Troubleshooting). If an install fails partway, rerun the same command with `--resume`.

When reporting a bug, include your logs (`stalker-gamma debug logs` opens the folder), the command you ran, and the output of `stalker-gamma --version`.

## Development

### Requirements

- .NET 10 SDK
- Native dependencies in `stalker-gamma-cli/bin/Debug/net10.0/`:
  - `libcurl-impersonate` (`.dll`, `.so` or `.dylib`) and `cacert.pem` next to the executable
  - `resources/7zz` (`resources/7zz.exe` and `7z.dll` on Windows)

The easiest way to get these is to run your platform's build below once, then copy them from its output folder.

### Build

| Platform | Commands                       | Output                                   |
| -------- | ------------------------------ | ---------------------------------------- |
| Windows  | `cd build/win; build.bat`      | `build/win/build/stalker-gamma-cli`      |
| Linux    | `cd build/linux && make build` | `build/linux/build/stalker-gamma-cli`    |
| macOS    | `cd build/mac && make build`   | `build/mac/build/stalker-gamma-cli`      |
