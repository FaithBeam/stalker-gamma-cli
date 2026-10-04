#!/usr/bin/env python3
"""HTTP API that loads URLs in a SeleniumBase-driven Chrome/Chromium browser.

One browser is started at launch and reused for every request. All browser
work runs on a single worker thread, so requests are handled one at a time.
"""

import argparse
import asyncio
import base64
import multiprocessing
import sys
import time
from concurrent.futures import Future, ThreadPoolExecutor
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException, Response
from mycdp import fetch, network
from pydantic import BaseModel
from seleniumbase import SB


class NavigateResponseDto(BaseModel):
    status_code: int
    url: str
    content: str
    headers: dict[str, Any | None]


class NavigateRequestDto(BaseModel):
    url: str
    follow_redirects: bool = True


@dataclass
class CapturedResponse:
    status: int
    url: str
    headers: dict[str, str]
    content: str


@dataclass
class Navigation:
    """Responses recorded for the navigation currently in progress."""

    follow_redirects: bool
    responses: list[CapturedResponse] = field(default_factory=list)

    def result(self):
        """Return the response this navigation ends on, or None if it's
        still pending. Cloudflare challenges never count; redirects only
        count when they aren't being followed."""
        for response in self.responses:
            if is_cf_challenge(response):
                continue
            if self.follow_redirects and is_redirect(response.status):
                continue
            return response
        return None


def is_redirect(status):
    return 300 <= status < 400


def is_cf_challenge(response):
    """Cloudflare marks its challenge pages with "cf-mitigated: challenge"."""
    return response.headers.get("cf-mitigated") == "challenge"


def decode_body(body, base64_encoded, headers):
    raw = base64.b64decode(body) if base64_encoded else body.encode()
    content_type = headers.get("content-type", "")
    charset = "utf-8"
    for part in content_type.split(";")[1:]:
        key, _, value = part.strip().partition("=")
        if key.lower() == "charset" and value:
            charset = value.strip("\"'")
    try:
        return raw.decode(charset, errors="replace")
    except LookupError:
        return raw.decode("utf-8", errors="replace")


# Markup found on Cloudflare challenge pages (mirrors SeleniumBase's checks).
CHALLENGE_MARKERS = (
    "/challenge-platform/h/",
    'id="challenge-widget-',
    "challenges.cloudflare.com",
    "cf-turnstile-",
)


def on_challenge_page(sb):
    """Return True if a Cloudflare challenge is showing, None if unsure."""
    source = sb.cdp.get_page_source()
    if len(source) < 400:
        return None  # page still loading
    if sb.cdp.get_title().startswith("Just a moment"):
        return True
    return any(marker in source for marker in CHALLENGE_MARKERS)


def pass_challenge(sb, timeout, done, poll=0.5, click_every=2):
    """Wait until done() is true, clicking any Cloudflare challenge as
    needed. Returns True on success, False at timeout."""
    deadline = time.monotonic() + timeout
    next_click = 0.0
    while time.monotonic() < deadline:
        if done():
            return True
        now = time.monotonic()
        # The checkbox iframe loads asynchronously, so retry the click
        # periodically rather than once.
        if now >= next_click and on_challenge_page(sb):
            sb.solve_captcha()
            next_click = now + click_every
        sb.sleep(poll)
    return False


# Where a Chrome for Testing build bundled in a "chrome" folder next to this
# executable keeps its binary, per platform.
BUNDLED_CHROME_BINARIES = {
    "linux": "chrome",
    "win32": "chrome.exe",
    "darwin": "Google Chrome for Testing.app/Contents/MacOS/"
    "Google Chrome for Testing",
}


def bundled_chrome():
    """Return the path of a Chrome bundled next to the executable, or None."""
    binary = BUNDLED_CHROME_BINARIES.get(sys.platform)
    if binary is None:
        return None
    base = Path(sys.executable if getattr(sys, "frozen", False) else __file__)
    path = base.resolve().parent / "chrome" / binary
    return str(path) if path.is_file() else None


class Browser:
    """A long-lived Chrome/Chromium in CDP mode, confined to one worker
    thread."""

    def __init__(self, headless, challenge_timeout, browser="chrome",
                 browser_path=None):
        self.headless = headless
        self.challenge_timeout = challenge_timeout
        self.browser = browser
        self.browser_path = browser_path
        self._executor = ThreadPoolExecutor(max_workers=1)
        self._context = None
        self._sb = None
        self._navigation: Navigation | None = None
        self.started: Future = self._executor.submit(self._start)

    @property
    def ready(self):
        return self.started.done() and self.started.exception() is None

    def _start(self):
        # SeleniumBase's CDP mode drives its own event loop on this thread.
        asyncio.set_event_loop(asyncio.new_event_loop())
        self._context = SB(
            uc=True,
            test=False,
            headless=self.headless,
            # A custom binary path takes precedence. Otherwise "chromium"
            # uses SeleniumBase's own Chromium, downloading it if missing.
            binary_location=self.browser_path,
            use_chromium=self.browser == "chromium" and not self.browser_path,
            # Stop Chrome from first trying an https:// version of http://
            # URLs, so only the exact URL given is requested.
            disable_features="HttpsUpgrades",
        )
        self._sb = self._context.__enter__()
        # CDP mode drives Chrome over DevTools without WebDriver, which
        # avoids most bot detection and can click Turnstile.
        self._sb.activate_cdp_mode()
        self._intercept_documents()

    def _intercept_documents(self):
        """Pause each top-frame document response, recording it into the
        current navigation and cancelling redirects it isn't following."""
        tab = self._sb.cdp.page
        top_frame = tab.target.target_id

        async def on_paused(event):
            navigation = self._navigation
            # Skip iframes, e.g. Cloudflare's checkbox widget.
            if (
                navigation is None
                or event.frame_id != top_frame
                or not event.response_status_code
            ):
                await tab.send(fetch.continue_request(event.request_id))
                return
            status = event.response_status_code
            headers = {}
            for h in event.response_headers or []:
                name = h.name.lower()
                headers[name] = (
                    f"{headers[name]}, {h.value}" if name in headers else h.value
                )
            content = ""
            if not is_redirect(status):
                try:
                    body, base64_encoded = await tab.send(
                        fetch.get_response_body(event.request_id)
                    )
                    content = decode_body(body, base64_encoded, headers)
                except Exception:
                    pass
            navigation.responses.append(
                CapturedResponse(status, event.request.url, headers, content)
            )
            if is_redirect(status) and not navigation.follow_redirects:
                await tab.send(
                    fetch.fail_request(event.request_id, network.ErrorReason.ABORTED)
                )
                return
            await tab.send(fetch.continue_request(event.request_id))

        self._sb.cdp.add_handler(fetch.RequestPaused, on_paused)
        pattern = fetch.RequestPattern(
            resource_type=network.ResourceType.DOCUMENT,
            request_stage=fetch.RequestStage.RESPONSE,
        )
        self._sb.cdp.loop.run_until_complete(
            tab.send(fetch.enable(patterns=[pattern]))
        )

    def _navigate(self, url, follow_redirects):
        navigation = Navigation(follow_redirects)
        self._navigation = navigation
        try:
            self._sb.cdp.open(url)
            if not pass_challenge(
                self._sb,
                self.challenge_timeout,
                lambda: navigation.result() is not None,
            ):
                return None
            return navigation.result()
        finally:
            self._navigation = None

    def navigate(self, url, follow_redirects) -> CapturedResponse | None:
        return self._executor.submit(self._navigate, url, follow_redirects).result()

    def _stop(self):
        if self._context is not None:
            self._context.__exit__(None, None, None)

    def close(self):
        if self.ready:
            self._executor.submit(self._stop).result()
        self._executor.shutdown(wait=False, cancel_futures=True)


def create_app(headless=False, challenge_timeout=30.0, browser="chrome",
               browser_path=None):
    @asynccontextmanager
    async def lifespan(app):
        app.state.browser = Browser(
            headless, challenge_timeout, browser, browser_path
        )
        try:
            yield
        finally:
            app.state.browser.close()

    app = FastAPI(lifespan=lifespan)

    @app.get(path="/livez")
    def livez() -> dict[str, str]:
        return {"status": "ok"}

    @app.get(path="/readyz")
    def readyz(response: Response) -> dict[str, str]:
        started = app.state.browser.started
        if not started.done():
            response.status_code = 503
            return {"status": "starting"}
        if error := started.exception():
            response.status_code = 503
            return {"status": f"browser failed to start: {error}"}
        return {"status": "ok"}

    @app.post(path="/navigate", response_model=NavigateResponseDto)
    def navigate(request: NavigateRequestDto) -> NavigateResponseDto:
        browser = app.state.browser
        if not browser.ready:
            raise HTTPException(status_code=503, detail="Browser is not ready")
        url = request.url
        if "://" not in url:
            url = "https://" + url
        try:
            response = browser.navigate(url, request.follow_redirects)
        except Exception as e:
            raise HTTPException(status_code=502, detail=str(e))
        if response is None:
            raise HTTPException(
                status_code=504,
                detail=f"No response from {url} within "
                f"{browser.challenge_timeout:g}s",
            )
        if response.status >= 400:
            raise HTTPException(
                status_code=502, detail=f"{url} returned HTTP {response.status}"
            )
        return NavigateResponseDto(
            status_code=response.status,
            url=response.url,
            content=response.content,
            headers=response.headers,
        )

    return app


def main(argv=None):
    import uvicorn

    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument(
        "--headless", action="store_true", help="Run the browser without a window"
    )
    parser.add_argument(
        "--challenge-timeout",
        type=float,
        default=30,
        metavar="SECONDS",
        help="Max seconds to wait for a page, including any Cloudflare "
        "challenge, before /navigate gives up (default: 30)",
    )
    parser.add_argument(
        "--browser",
        choices=("chrome", "chromium"),
        default="chrome",
        help="Browser to drive. \"chromium\" downloads SeleniumBase's "
        "Chromium build on first use unless --browser-path is given "
        "(default: chrome)",
    )
    parser.add_argument(
        "--browser-path",
        metavar="PATH",
        help="Path to a Chrome or Chromium executable to use instead of "
        "the auto-detected one (default: chrome/ next to this program if "
        "present, otherwise the installed Chrome)",
    )
    args = parser.parse_args(argv)
    if args.browser_path is None and args.browser == "chrome":
        args.browser_path = bundled_chrome()
    uvicorn.run(
        app=create_app(
            args.headless, args.challenge_timeout, args.browser, args.browser_path
        ),
        host=args.host,
        port=args.port,
    )


if __name__ == "__main__":
    multiprocessing.freeze_support()
    main()
