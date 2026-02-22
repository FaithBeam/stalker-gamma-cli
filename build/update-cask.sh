#!/bin/bash

if [[ -z "$1" || -z "$2" || -z "$3" || -z "$4" ]]; then
    echo "Error: An argument is missing or empty." >&2
    exit 1
fi

VERSION=$1
ARM64_SUM=$2
X64_SUM=$3
OUTPUT_PATH=$4

HOMEBREW_CASK=$(cat <<EOF
cask "stalker-gamma" do
  arch arm: "arm64", intel: "x64"

  version "${VERSION}"

  if Hardware::CPU.arm?
    sha256 "${ARM64_SUM}"
  else
    sha256 "${X64_SUM}"
  end

  url "https://github.com/FaithBeam/stalker-gamma-cli/releases/download/#{version}/stalker-gamma+mac.#{arch}.tar.gz"
  name "stalker-gamma"
  desc "Install Stalker GAMMA via CLI"
  homepage "https://github.com/FaithBeam/stalker-gamma-cli"

  depends_on formula: "libidn2"
  depends_on formula: "zstd"

  binary "stalker-gamma"

  postflight do
    system_command "/usr/bin/xattr",
                   args:         ["-rd", "com.apple.quarantine", "#{staged_path}/"],
                   print_stderr: false
  end

  zap trash: ""
end
EOF
)

echo "$HOMEBREW_CASK" > "$OUTPUT_PATH"