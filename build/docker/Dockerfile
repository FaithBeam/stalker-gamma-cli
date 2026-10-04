# load_url (python-cli) on Alpine, for linux/amd64 and linux/arm64.
#
# Chrome for Testing (what load_url.mk bundles) is linked against glibc and
# won't run on Alpine's musl, so this uses Alpine's own Chromium and its
# matching chromedriver instead. main.py is run directly; no PyInstaller.
#
# Build from the repo root:
#   docker buildx build --platform linux/amd64,linux/arm64 \
#       -f build/docker/Dockerfile -t load_url .
# Run:
#   docker run --rm -p 8000:8000 --shm-size=1g load_url

FROM python:3.14-alpine3.24

# chromium + chromium-chromedriver come from the same aport, so their major
# versions always match. xvfb gives Chromium a virtual display: SeleniumBase's
# UC mode starts it on Linux when no display exists, which gets past
# Cloudflare far more reliably than --headless.
RUN apk add --no-cache \
        chromium \
        chromium-chromedriver \
        xvfb \
        font-noto \
        tini

WORKDIR /app

COPY python-cli/requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY python-cli/main.py ./

# SeleniumBase may write a patched driver into --driver-dir, so give it a
# writable copy instead of pointing it at /usr/bin.
RUN adduser -D load_url \
    && mkdir /app/chromedriver \
    && cp /usr/bin/chromedriver /app/chromedriver/ \
    && chown -R load_url:load_url /app

USER load_url

EXPOSE 8000

ENTRYPOINT ["/sbin/tini", "--", "python", "main.py", \
    "--host", "0.0.0.0", \
    "--browser", "chromium", \
    "--browser-path", "/usr/bin/chromium-browser", \
    "--driver-dir", "/app/chromedriver"]
