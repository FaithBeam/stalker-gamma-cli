#!/bin/bash

if [[ -z "$1" || -z "$2" || -z "$3" || -z "$4" ]]; then
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
        PKGNAME="stalker-gamma-cli-bin"
        PKGDESC="a cli to install Stalker Anomaly and the GAMMA mod pack (appimage)"
        ASSET="stalker-gamma"
        BINARY="stalker-gamma"
        DEPENDS="'unzip' 'fuse2'"
        ;;
    server)
        PKGNAME="stalker-gamma-server-bin"
        PKGDESC="companion server for stalker-gamma-cli (appimage)"
        ASSET="stalker-gamma-server"
        BINARY="stalker-gamma-server"
        DEPENDS="'fuse2'"
        ;;
    *)
        echo "Error: package must be 'cli' or 'server', got '$PACKAGE'." >&2
        exit 1
        ;;
esac

PKGBUILD=$(cat <<EOF
pkgname=${PKGNAME}
pkgver=${VERSION}
pkgrel=1
pkgdesc="${PKGDESC}"
arch=('x86_64' 'aarch64')
url="https://github.com/FaithBeam/stalker-gamma-cli"
license=('GPL-3.0-or-later')
options=(!strip)
depends=(${DEPENDS})
source_x86_64=("${ASSET}+linux.x64-\${pkgver}.AppImage::https://github.com/FaithBeam/stalker-gamma-cli/releases/download/\${pkgver}/${ASSET}+linux.x64.AppImage")
source_aarch64=("${ASSET}+linux.arm64-\${pkgver}.AppImage::https://github.com/FaithBeam/stalker-gamma-cli/releases/download/\${pkgver}/${ASSET}+linux.arm64.AppImage")
sha256sums_x86_64=('${X64_SUM}')
sha256sums_aarch64=('${ARM64_SUM}')

package() {
  install -Dm755 ${ASSET}+linux.*.AppImage "\${pkgdir}/usr/bin/${BINARY}"
}
EOF
)
  
echo "$PKGBUILD" > "$OUTPUT_PATH"
