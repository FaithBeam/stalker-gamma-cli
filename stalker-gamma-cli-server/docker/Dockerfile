FROM python:3.14-slim-bookworm

# playwright must match the major.minor of the Microsoft.Playwright client (1.62).
# indexed_zstd (via fpgen) has no prebuilt wheel, so build tools are installed for the
# pip step only and removed afterwards. Only the libraries Firefox links against are
# kept; Camoufox renders with its own bundled fonts, so no system fonts are needed.
RUN apt-get update \
    && apt-get install -y --no-install-recommends g++ libzstd-dev \
    && pip install --no-cache-dir "camoufox[geoip]==0.5.7" "playwright==1.62.0" \
    && apt-get purge -y --auto-remove g++ libzstd-dev \
    && apt-get install -y --no-install-recommends libgtk-3-0 libx11-xcb1 libasound2 libdbus-glib-1-2 \
    && rm -rf /var/lib/apt/lists/*

# fpgen keeps its model inside its package directory, which camoufox fetch writes to.
RUN useradd --create-home camoufox \
    && chown -R camoufox "$(python -c 'import sysconfig; print(sysconfig.get_path("purelib"))')/fpgen"
USER camoufox
WORKDIR /home/camoufox

# The OS the browser fingerprints as: windows, macos or linux. The browser bundle ships
# fonts for all three (~2.1GB); only the font groups this OS reads are kept.
ARG TARGET_OS=windows
ENV CAMOUFOX_OS=${TARGET_OS}

# Bake the browser, GeoIP database and default addons into the image.
RUN python -m camoufox fetch \
    && python - <<'EOF'
import json, os, shutil, glob
key = {"windows": "win", "macos": "mac", "linux": "lin"}[os.environ["CAMOUFOX_OS"]]
for fonts in glob.glob(os.path.expanduser("~/.cache/camoufox/browsers/*/*/fonts")):
    groups = json.load(open(os.path.join(fonts, "groups.json")))
    for group in set(groups["groups"]) - set(groups["readBy"][key]):
        shutil.rmtree(os.path.join(fonts, group))
EOF

COPY server.py .

ENV PORT=9222 WS_PATH=camoufox
EXPOSE 9222

CMD ["python", "-u", "server.py"]
