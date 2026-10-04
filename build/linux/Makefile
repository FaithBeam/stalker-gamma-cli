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

# Chrome for Testing: Google's portable (zip, no installer) Chrome builds.
# Latest stable version: https://googlechromelabs.github.io/chrome-for-testing/LATEST_RELEASE_STABLE
CHROME_VERSION ?= 154.0.8037.92
ifeq ($(ARCH),x64)
    CHROME_ARCH := linux64
else ifeq ($(ARCH),arm64)
    CHROME_ARCH := linux-arm64
else
    CHROME_ARCH := unknown
endif
CHROME_BUILD_DIR = $(BUILD_DIR)/chrome
CHROME_ARCHIVE = $(CHROME_BUILD_DIR)/chrome-$(CHROME_ARCH).zip
CHROME_DL_URL = https://storage.googleapis.com/chrome-for-testing-public/$(CHROME_VERSION)/$(CHROME_ARCH)/chrome-$(CHROME_ARCH).zip
# chromedriver for the same Chrome version
CHROMEDRIVER_ARCHIVE = $(CHROME_BUILD_DIR)/chromedriver-$(CHROME_ARCH).zip
CHROMEDRIVER_DL_URL = https://storage.googleapis.com/chrome-for-testing-public/$(CHROME_VERSION)/$(CHROME_ARCH)/chromedriver-$(CHROME_ARCH).zip

STALKER_GAMMA_BUILD_DIR = $(BUILD_DIR)/stalker-gamma-cli
STALKER_RESOURCES_DIR = $(STALKER_GAMMA_BUILD_DIR)/resources
STALKER_GAMMA_CLI_PROJECT = $(ROOT)/stalker-gamma-cli/stalker-gamma-cli.csproj

PYTHON_CLI_BUILD_DIR = $(BUILD_DIR)/python-cli
PYTHON_CLI_SPEC = $(ROOT)/python-cli/main.spec
PYTHON_CLI_REQUIREMENTS = $(ROOT)/python-cli/requirements.txt
PYTHON_CLI_REQUIREMENTS_DEV = $(ROOT)/python-cli/requirements-dev.txt
PYTHON_CLI_VENV = $(BUILD_DIR)/python-cli-venv
PYTHON_CLI_PYINSTALLER = $(PYTHON_CLI_VENV)/bin/pyinstaller

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

	# chrome
	mkdir $(CHROME_BUILD_DIR)
	curl -fLo $(CHROME_ARCHIVE) $(CHROME_DL_URL)
	unzip -q $(CHROME_ARCHIVE) -d $(CHROME_BUILD_DIR)
	curl -fLo $(CHROMEDRIVER_ARCHIVE) $(CHROMEDRIVER_DL_URL)
	unzip -q $(CHROMEDRIVER_ARCHIVE) -d $(CHROME_BUILD_DIR)

	# python-cli
	python3 -m venv $(PYTHON_CLI_VENV)
	$(PYTHON_CLI_VENV)/bin/pip install -r $(PYTHON_CLI_REQUIREMENTS)
	$(PYTHON_CLI_VENV)/bin/pip install -r $(PYTHON_CLI_REQUIREMENTS_DEV)
	mkdir $(PYTHON_CLI_BUILD_DIR)
	$(PYTHON_CLI_PYINSTALLER) $(ROOT)/python-cli/main.py --onedir --name=load_url --collect-all=seleniumbase --noconfirm --clean --distpath $(PYTHON_CLI_BUILD_DIR)

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
	cp -r $(PYTHON_CLI_BUILD_DIR)/load_url/* $(STALKER_RESOURCES_DIR)/
	# load_url looks for chrome/chrome next to itself
	cp -r $(CHROME_BUILD_DIR)/chrome-$(CHROME_ARCH) $(STALKER_RESOURCES_DIR)/chrome
	# load_url points SeleniumBase at chromedriver/
	mkdir $(STALKER_RESOURCES_DIR)/chromedriver
	cp $(CHROME_BUILD_DIR)/chromedriver-$(CHROME_ARCH)/chromedriver $(STALKER_RESOURCES_DIR)/chromedriver/
	cp $(CUR_DIR)/README.md $(STALKER_GAMMA_BUILD_DIR)/

archive:
	tar -cvJf stalker-gamma-cli.tar.xz --directory=$(STALKER_GAMMA_BUILD_DIR) .

clean:
	rm -rf $(BUILD_DIR)
