VERSION ?= 1.0.0
ARCH ?= x64

CUR_DIR = $(realpath .)
ROOT = $(realpath ../../)

BUILD_DIR = $(CUR_DIR)/build

CURL_IMPERSONATE_VERSION = v2.2.3
ifeq ($(ARCH),x64)
    CURL_ARCH := x86_64
else ifeq ($(ARCH),arm64)
    CURL_ARCH := aarch64
else
    CURL_ARCH := unknown
endif
CURL_BUILD_DIR = $(BUILD_DIR)/curl-impersonate
CURL_IMPERSONATE_ARCHIVE = $(CURL_BUILD_DIR)/libcurl-impersonate-$(CURL_IMPERSONATE_VERSION).$(CURL_ARCH)-linux-gnu.tar.gz
CURL_IMPERSONATE_DL_URL = https://github.com/lexiforest/curl-impersonate/releases/download/$(CURL_IMPERSONATE_VERSION)/libcurl-impersonate-$(CURL_IMPERSONATE_VERSION).$(CURL_ARCH)-linux-gnu.tar.gz

7Z_VERSION = 2603
ifeq ($(ARCH),x64)
    7Z_ARCH := x64
else ifeq ($(ARCH),arm64)
    7Z_ARCH := arm64
else
    7Z_ARCH := unknown
endif
7Z_BUILD_DIR = $(BUILD_DIR)/7zip
7Z_ARCHIVE = $(7Z_BUILD_DIR)/7z$(7Z_VERSION)-linux-$(7Z_ARCH).tar.xz
7Z_DL_URL = https://www.7-zip.org/a/7z$(7Z_VERSION)-linux-$(7Z_ARCH).tar.xz

STALKER_GAMMA_BUILD_DIR = $(BUILD_DIR)/stalker-gamma-cli
STALKER_RESOURCES_DIR = $(STALKER_GAMMA_BUILD_DIR)/resources
STALKER_GAMMA_CLI_PROJECT = $(ROOT)/stalker-gamma-cli/stalker-gamma-cli.csproj

CLOUDSCRAPER_BUILD_DIR = $(BUILD_DIR)/cloudscraper
CLOUDSCRAPER_SPEC = $(ROOT)/python-api/main.spec
CLOUDSCRAPER_REQUIREMENTS = $(ROOT)/python-api/requirements.txt
CLOUDSCRAPER_VENV = $(BUILD_DIR)/cloudscraper-venv
CLOUDSCRAPER_PYINSTALLER = $(CLOUDSCRAPER_VENV)/bin/pyinstaller

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

	# cloudscraper
	python3 -m venv $(CLOUDSCRAPER_VENV)
	$(CLOUDSCRAPER_VENV)/bin/pip install -r $(CLOUDSCRAPER_REQUIREMENTS)
	mkdir $(CLOUDSCRAPER_BUILD_DIR)
	$(CLOUDSCRAPER_PYINSTALLER) --distpath $(CLOUDSCRAPER_BUILD_DIR) $(CLOUDSCRAPER_SPEC)

	# build stalker-gamma-cli
	mkdir $(STALKER_GAMMA_BUILD_DIR)
	dotnet publish -c Release $(STALKER_GAMMA_CLI_PROJECT) -o $(STALKER_GAMMA_BUILD_DIR) -p:AssemblyVersion=$(VERSION)

	# cleanup stalker-gamma-cli debug files
	rm $(STALKER_GAMMA_BUILD_DIR)/*.pdb
	rm $(STALKER_GAMMA_BUILD_DIR)/*.dbg

	# copy dependency artifacts to stalker-gamma-cli
	mkdir $(STALKER_RESOURCES_DIR)
	curl -Lo $(STALKER_GAMMA_BUILD_DIR)/cacert.pem "https://curl.se/ca/cacert.pem"
	cp $(CURL_BUILD_DIR)/libcurl-impersonate.so.4.8.0 $(STALKER_GAMMA_BUILD_DIR)/libcurl-impersonate.so
	cp $(7Z_BUILD_DIR)/7zz $(STALKER_RESOURCES_DIR)/
	cp -r $(CLOUDSCRAPER_BUILD_DIR)/cloudscraper $(STALKER_RESOURCES_DIR)/
	cp $(CUR_DIR)/README.md $(STALKER_GAMMA_BUILD_DIR)/

archive:
	tar -cvJf stalker-gamma-cli.tar.xz --directory=$(STALKER_GAMMA_BUILD_DIR) .

clean:
	rm -rf $(BUILD_DIR)
