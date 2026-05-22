import argparse
import multiprocessing
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import cloudscraper
from typing import Any

app = FastAPI()

scraper = cloudscraper.create_scraper(browser={
       "custom": f"stalker-gamma/1.0.0"
    })  

class NavigateResponseDto(BaseModel):
    status_code: int
    url: str
    content: str
    headers: dict[str, Any | None]


class NavigateRequestDto(BaseModel): 
    url: str
    follow_redirects: bool = True


@app.get(path="/livez")
def livez() -> dict[str, str]:
    return {"status": "ok"}


@app.get(path="/readyz")
def readyz() -> dict[str, str]:
    return {"status": "ok"}


@app.post(path="/navigate", response_model=NavigateResponseDto)
def navigate(request: NavigateRequestDto) -> NavigateResponseDto:
    try:
        response = scraper.get(url=request.url, allow_redirects=request.follow_redirects)
        response.raise_for_status()
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(object=e))
    return NavigateResponseDto(
        status_code=response.status_code,
        url=response.url,
        content=response.text,
        headers=dict(response.headers)
    )


if __name__ == "__main__":
    multiprocessing.freeze_support()
    import uvicorn
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()
    uvicorn.run(app=app, host=args.host, port=args.port)
