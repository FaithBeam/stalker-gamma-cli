## Requirements

- Windows with PowerShell
- .NET 10 SDK (10.0.403 or newer)
- Visual Studio 2022 or newer, or Build Tools for Visual Studio, with the **Desktop development with C++** workload. The CLI is compiled with Native AOT, which needs the MSVC compiler and linker.
  - For `arm64` builds, also install the **MSVC ARM64 build tools** component

The script downloads 7-Zip and curl-impersonate itself.

## How to

From this folder, run:

```bat
build.bat [version] [arch]
```

- `version` is stamped into the binaries. Defaults to `1.0.0`.
- `arch` is `x64` or `arm64`. Defaults to `x64`.

Each run deletes `build\` and starts fresh.

## Output

- `build\stalker-gamma-cli\`: the CLI, with `libcurl-impersonate.dll`, `cacert.pem` and `resources\7z.dll`
- `build\stalker-gamma-server\`: the server
- `stalker-gamma+win.{arch}.zip` and `stalker-gamma-server+win.{arch}.zip` in the folder you ran it from

See [Build Information](https://github.com/FaithBeam/stalker-gamma-cli/wiki/Build-Information) on the wiki for all platforms.
