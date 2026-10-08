#!/bin/bash
set -euo pipefail

if [[ $# -lt 4 || -z "$1" || -z "$2" || -z "$3" || -z "$4" ]]; then
    echo "Usage: $0 <version> <arm64-sha256> <x64-sha256> <output-path> [cli|server]" >&2
    exit 1
fi

VERSION=$1
ARM64_SUM=$2
X64_SUM=$3
OUTPUT_PATH=$4
PACKAGE=${5:-cli}

case "$PACKAGE" in
    cli)
        TOKEN="stalker-gamma"
        DESC="Install Stalker GAMMA via CLI"
        ASSET="stalker-gamma"
        BINARY="stalker-gamma"
        # libcurl-impersonate needs these at runtime
        DEPENDS=$'  depends_on formula: "libidn2"\n  depends_on formula: "zstd"\n\n'
        ;;
    server)
        TOKEN="stalker-gamma-server"
        DESC="Companion server for stalker-gamma-cli"
        ASSET="stalker-gamma-server"
        BINARY="stalker-gamma-server"
        DEPENDS=""
        ;;
    *)
        echo "Error: package must be 'cli' or 'server', got '$PACKAGE'." >&2
        exit 1
        ;;
esac

mkdir -p "$(dirname "$OUTPUT_PATH")"

cat > "$OUTPUT_PATH" <<EOF
cask "${TOKEN}" do
  arch arm: "arm64", intel: "x64"

  version "${VERSION}"
  sha256 arm:   "${ARM64_SUM}",
         intel: "${X64_SUM}"

  url "https://github.com/FaithBeam/stalker-gamma-cli/releases/download/#{version}/${ASSET}+mac.#{arch}.tar.gz"
  name "${TOKEN}"
  desc "${DESC}"
  homepage "https://github.com/FaithBeam/stalker-gamma-cli"

${DEPENDS}  binary "${BINARY}"

  postflight_steps do
     on_macos do
       run "/usr/bin/xattr", args: ["-dr", "com.apple.quarantine", "{{staged_path}}/"]
     end
  end
end
EOF
