# Builds load_url (python-cli) and bundles Chrome for Testing and chromedriver
# with it. Output: $(LOAD_URL_BUNDLE_DIR).
#
# Usage: make -f load_url.mk build ARCH=x64

ARCH ?= x64

CUR_DIR = $(realpath .)
ROOT = $(realpath ../../)

# Kept apart from the main Makefile's build/ so the two never collide.
BUILD_DIR ?= $(CUR_DIR)/build-load_url
LOAD_URL_BUNDLE_DIR = $(BUILD_DIR)/load_url-bundle

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

PYTHON_CLI_BUILD_DIR = $(BUILD_DIR)/python-cli
PYTHON_CLI_REQUIREMENTS = $(ROOT)/python-cli/requirements.txt
PYTHON_CLI_REQUIREMENTS_DEV = $(ROOT)/python-cli/requirements-dev.txt
PYTHON_CLI_VENV = $(BUILD_DIR)/python-cli-venv
PYTHON_CLI_PYINSTALLER = $(PYTHON_CLI_VENV)/bin/pyinstaller

build:
	mkdir -p $(BUILD_DIR)

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

	# bundle
	cp -R $(PYTHON_CLI_BUILD_DIR)/load_url $(LOAD_URL_BUNDLE_DIR)
	# load_url looks for chrome/chrome next to itself
	cp -R $(CHROME_BUILD_DIR)/chrome-$(CHROME_ARCH) $(LOAD_URL_BUNDLE_DIR)/chrome
	# load_url points SeleniumBase at chromedriver/
	mkdir $(LOAD_URL_BUNDLE_DIR)/chromedriver
	cp $(CHROME_BUILD_DIR)/chromedriver-$(CHROME_ARCH)/chromedriver $(LOAD_URL_BUNDLE_DIR)/chromedriver/

clean:
	rm -rf $(BUILD_DIR)
