VERSION ?= 1.0.0

CUR_DIR = $(realpath .)
ROOT = $(realpath ../../)

BUILD_DIR = $(CUR_DIR)/build

DOTNET_INSTALL_URL = https://dot.net/v1/dotnet-install.sh
DOTNET_INSTALL_SCRIPT_PATH = $(BUILD_DIR)/dotnet-install.sh
DOTNET_INSTALL_PATH = $(BUILD_DIR)/.dotnet

CURL_IMPERSONATE_VERSION = v1.2.5
CURL_BUILD_DIR = $(BUILD_DIR)/curl-impersonate
CURL_IMPERSONATE_ARCHIVE = $(CURL_BUILD_DIR)/curl-impersonate-$(CURL_IMPERSONATE_VERSION).arm64-macos.tar.gz
CURL_IMPERSONATE_DL_URL = https://github.com/lexiforest/curl-impersonate/releases/download/$(CURL_IMPERSONATE_VERSION)/curl-impersonate-$(CURL_IMPERSONATE_VERSION).arm64-macos.tar.gz

7Z_VERSION = 2501
7Z_BUILD_DIR = $(BUILD_DIR)/7zip
7Z_ARCHIVE = $(7Z_BUILD_DIR)/7z$(7Z_VERSION)-mac.tar.xz
7Z_DL_URL = https://7-zip.org/a/7z$(7Z_VERSION)-mac.tar.xz

STALKER_GAMMA_BUILD_DIR = $(BUILD_DIR)/stalker-gamma-cli
STALKER_RESOURCES_DIR = $(STALKER_GAMMA_BUILD_DIR)/resources
STALKER_GAMMA_CLI_PROJECT = $(ROOT)/stalker-gamma-cli/stalker-gamma-cli.csproj

build:
	mkdir $(BUILD_DIR)

	# curl-impersonate
	mkdir $(CURL_BUILD_DIR)
	curl -Lo $(CURL_IMPERSONATE_ARCHIVE) $(CURL_IMPERSONATE_DL_URL)
	tar -xvzf $(CURL_IMPERSONATE_ARCHIVE) -C $(CURL_BUILD_DIR)

	# 7zip
	mkdir $(7Z_BUILD_DIR)
	curl -Lo $(7Z_ARCHIVE) $(7Z_DL_URL)
	tar -xvf $(7Z_ARCHIVE) -C $(7Z_BUILD_DIR)
	
	# dotnet-install
    curl $(DOTNET_INSTALL_URL) -Lo $(DOTNET_INSTALL_SCRIPT_PATH)
    chmod +x $(DOTNET_INSTALL_SCRIPT_PATH)
    $(DOTNET_INSTALL_SCRIPT_PATH) -Channel 10.0 -InstallDir $(DOTNET_INSTALL_PATH)
    export PATH=$(DOTNET_INSTALL_PATH):$PATH
    export DOTNET_ROOT=$(DOTNET_INSTALL_PATH)
	
	# build stalker-gamma-cli
	mkdir $(STALKER_GAMMA_BUILD_DIR)
	dotnet publish -c Release $(STALKER_GAMMA_CLI_PROJECT) -o $(STALKER_GAMMA_BUILD_DIR) -p:AssemblyVersion=$(VERSION)

	# cleanup stalker-gamma-cli debug files
	rm $(STALKER_GAMMA_BUILD_DIR)/*.pdb
	rm -rf $(STALKER_GAMMA_BUILD_DIR)/*.dSYM

	# copy dependency artifacts to stalker-gamma-cli
	mkdir $(STALKER_RESOURCES_DIR)
	curl -Lo $(STALKER_RESOURCES_DIR)/cacert.pem "https://curl.se/ca/cacert.pem"
	cp $(CURL_BUILD_DIR)/curl-impersonate $(STALKER_RESOURCES_DIR)/
	cp $(7Z_BUILD_DIR)/7zz $(STALKER_RESOURCES_DIR)/
	cp $(CUR_DIR)/README.md $(STALKER_GAMMA_BUILD_DIR)/

archive:
	tar -cvJf stalker-gamma-cli.tar.xz --directory=$(STALKER_GAMMA_BUILD_DIR) .

clean:
	rm -rf $(BUILD_DIR)
