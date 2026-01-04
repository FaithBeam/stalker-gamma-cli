# Requirements

You need these applications installed to run stalker-gamma:

1. git
2. libidn2
3. zstd

You should install them with homebrew:

`brew install git libidn2 zstd`

# Usage

1. Remove the quarantine attribute from the binaries: `xattr -dr com.apple.quarantine .`
2. Create a config:

   ```bash
   ./stalker-gamma config create \
   --anomaly gamma/anomaly \
   --gamma gamma/gamma \
   --cache gamma/cache \
   --download-threads 4
   ```
3. Install Anomaly and GAMMA:

    ```bash
   ./stalker-gamma full-install
   ```
    
After install your folder will look like this:
    
```bash
.
├── gamma
│   ├── anomaly
│   ├── cache
│   └── gamma
```

4. Set your WINE prefix to run gamma/ModOrganizer.exe
5. Install these dependencies with winetricks into your prefix:
   - `winetricks d3dcompiler_43 d3dcompiler_47 d3dx10 d3dx11_43 d3dx9 vcrun2022` 