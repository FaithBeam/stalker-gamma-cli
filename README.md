# stalker-gamma-cli

_stalker-gamma-cli_ is a cli to install Stalker Anomaly and the GAMMA mod pack.

```
❯ stalker-gamma
Usage: [command] [-h|--help] [--version]

Commands:
  anomaly-install                    Installs Stalker Anomaly.
  check-anomaly                      Verifies the integrity of Stalker Anomaly
  config                             Get the currently active profile
  config create                      Create settings file
  config delete                      Delete a profile. If this profile was active, you should set another to be active with config use
  config list                        List profiles
  config use                         Set a profile as active.
  debug hash-install                 For debugging broken installations only. Hashes installation folders and creates a compressed archive containing the computed hashes.
  full-install                       This will install/update Anomaly and all GAMMA addons.
  mo2 config get selected-profile    Retrieves the selected profile information from the ModOrganizer.ini file within the specified directory.
  mo2 config set selected-profile    Updates the selected profile in the ModOrganizer.ini file for the specified directory.
  mo2 mod delete                     Deletes a specified mod in the provided profile.
  mo2 mod disable                    Disables a specified mod in the given profile
  mo2 mod enable                     Enables a specified mod within a given profile.
  mo2 mod status                     Show mod status info
  mo2 profile delete                 Deletes a specified profile from a Gamma installation.
  mo2 profile list mods              Lists all mods in a profile.
  mo2 profiles list                  Lists all profiles in a gamma installation.
  update apply                       Apply any updates
  update check                       Check for updates
```

## Features

- Install Stalker Anomaly
- Install GAMMA mod pack
- Manage mod lists for GAMMA
- Update support

## Usage

### Install

| OS | Method                                                                                 |
|----------|----------------------------------------------------------------------------------------|
| Windows  | [Windows Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Windows-Install) |
| Linux    | [Linux Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Linux-Install)     |
| macOS    | [MacOS Install](https://github.com/FaithBeam/stalker-gamma-cli/wiki/MacOS-Install)     |

### Update

After you've performed a full-install, you can update your installation.

Check for updates and apply:

```bash
stalker-gamma update check
stalker-gamma update apply
```

## Build

| Platform | Commands                       |
|----------|--------------------------------|
| Windows  | `cd build/win; build.bat`      |
| Linux    | `cd build/linux && make build` |
| macOS    | `cd build/mac && make build`   |

Artifacts in the `build/stalker-gamma-cli` folder.

## Development

### Requirements

- .NET 10 SDK
- 7zz, curl-impersonate, cacert.pem in stalker-gamma-cli/bin/debug/net10.0/resources folder
  - These dependencies can be retrieved by running your platform's `make build` command and copying the `resources` folder to the proper location
