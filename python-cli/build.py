#!/usr/bin/env python3
"""Package main.py into an executable with PyInstaller.

Default output is a folder: dist/load_url/, run via dist/load_url/load_url
(or dist\\load_url\\load_url.exe on Windows). Ship the whole folder.

Pass --onefile for a single dist/load_url[.exe] instead. It is much slower
to start, because it unpacks itself to a new temp folder on every run.

PyInstaller can't cross-compile, so build on the OS you're targeting.
"""

import sys

import PyInstaller.__main__

mode = "--onefile" if "--onefile" in sys.argv[1:] else "--onedir"

PyInstaller.__main__.run(
    [
        "main.py",
        mode,
        "--name=load_url",
        # SeleniumBase loads modules, JS and config files dynamically.
        "--collect-all=seleniumbase",
        "--noconfirm",
        "--clean",
    ]
)
