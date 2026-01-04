# Requirements

You need these applications installed to run stalker-gamma:

1. git
2. unzip

Refer to your distro how to install them if they're not installed.

# Usage

1. Create a config:

   ```bash
   ./stalker-gamma config create \
   --anomaly gamma/anomaly \
   --gamma gamma/gamma \
   --cache gamma/cache \
   --download-threads 4
   ```
2. Install Anomaly and GAMMA:

    ```bash
   ./stalker-gamma full-install
   ```
   
Your directory structure should look like this:
    
```bash
.
├── gamma
│   ├── anomaly
│   ├── cache
│   └── gamma
```

3. Set your WINE prefix to run gamma/ModOrganizer.exe
4. Install these dependencies with winetricks into your prefix:
   - `winetricks d3dcompiler_43 d3dcompiler_47 d3dx10 d3dx11_43 d3dx9 vcrun2022`
