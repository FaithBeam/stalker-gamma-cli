#!/bin/bash

if [[ -z "$1" || -z "$2" || -z "$3" || -z "$4" ]]; then
    echo "Error: An argument is missing or empty." >&2
    exit 1
fi

VERSION=$1
ARM64_SUM=$2
X64_SUM=$3
OUTPUT_PATH=$4

PKGBUILD=$(cat <<EOF
pkgname=stalker-gamma-cli-bin
pkgver=${VERSION}
pkgrel=1
pkgdesc="a cli to install Stalker Anomaly and the GAMMA mod pack (appimage)"
arch=('x86_64' 'aarch64')
url="https://github.com/FaithBeam/stalker-gamma-cli"
license=('GPL-3.0-or-later')
options=(!strip)
depends=('unzip' 'fuse2')
source_x86_64=("stalker-gamma+linux.x64-\${pkgver}.AppImage::https://github.com/FaithBeam/stalker-gamma-cli/releases/download/\${pkgver}/stalker-gamma+linux.x64.AppImage")
source_aarch64=("stalker-gamma+linux.arm64-\${pkgver}.AppImage::https://github.com/FaithBeam/stalker-gamma-cli/releases/download/\${pkgver}/stalker-gamma+linux.arm64.AppImage")
sha256sums_x86_64=('${X64_SUM}')
sha256sums_aarch64=('${ARM64_SUM}')

package() {
  install -Dm755 stalker-gamma+linux.*.AppImage "$pkgdir/usr/bin/stalker-gamma"
}
EOF
)
  
echo "$PKGBUILD" > "$OUTPUT_PATH"