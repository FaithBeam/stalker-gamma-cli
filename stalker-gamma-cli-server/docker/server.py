import os

from camoufox.server import launch_server

launch_server(
    disable_coop=True,  # lets the Turnstile checkbox in its cross-origin iframe be clicked
    headless=True,
    os=os.environ.get("CAMOUFOX_OS", "windows"),  # must match the fonts kept in the image
    geoip=True,  # timezone/locale match the outgoing IP
    host="0.0.0.0",  # reachable from outside the container
    port=int(os.environ.get("PORT", "9222")),
    ws_path=os.environ.get("WS_PATH", "camoufox"),
    window=(1280, 720),
    i_know_what_im_doing=True,
)
