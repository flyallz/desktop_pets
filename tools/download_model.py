"""Resume a public model download; never sends local photos."""
from pathlib import Path
import hashlib
import sys
import requests

target = Path(__file__).resolve().parents[1] / ".build/models/isnet-general-use.onnx"
url = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx"
target.parent.mkdir(parents=True, exist_ok=True)
if target.exists() and hashlib.md5(target.read_bytes()).hexdigest() == "fc16ebd8b0c10d971d3513d564d01e29":
    print(f"Verified cached model: {target.stat().st_size} bytes", flush=True)
    sys.exit(0)
for attempt in range(3):
    try:
        offset = target.stat().st_size if target.exists() else 0
        with requests.get(url, headers={"Range": f"bytes={offset}-"}, stream=True, timeout=(15, 45)) as response:
            response.raise_for_status()
            resume = response.status_code == 206
            if resume and not response.headers.get("Content-Range", "").startswith(f"bytes {offset}-"):
                raise RuntimeError("Unexpected download range")
            with target.open("ab" if resume else "wb") as output:
                for chunk in response.iter_content(1024 * 1024):
                    output.write(chunk)
        digest = hashlib.md5(target.read_bytes()).hexdigest()
        if digest != "fc16ebd8b0c10d971d3513d564d01e29":
            raise RuntimeError("Model checksum mismatch")
        print(f"Verified model: {target.stat().st_size} bytes", flush=True)
        break
    except requests.RequestException as error:
        print(f"Download attempt {attempt + 1}: {type(error).__name__}", flush=True)
        if attempt == 2:
            raise
