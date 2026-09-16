"""Read one image from stdin; emit PNG to stdout. No photo files are stored."""
import io
import os
import sys
import warnings
from pathlib import Path

# Optional existing local dependency folder; no runtime downloads.
deps = os.environ.get("PET_PYTHON_DEPS")
if deps:
    sys.path.insert(0, deps)
from PIL import Image, ImageOps
import numpy as np

Image.MAX_IMAGE_PIXELS = 20_000_000
warnings.simplefilter("error", Image.DecompressionBombWarning)
raw = sys.stdin.buffer.read(10 * 1024 * 1024 + 1)
if len(raw) > 10 * 1024 * 1024:
    raise ValueError("Image is too large")
photo = ImageOps.exif_transpose(Image.open(io.BytesIO(raw))).convert("RGBA")
photo.thumbnail((1400, 1400), Image.Resampling.LANCZOS)
if photo.width < 32 or photo.height < 32:
    raise ValueError("Image is too small")

model = Path(os.environ.get("PET_MODEL_PATH", "models/isnet-general-use.onnx"))
if not model.is_file():
    raise FileNotFoundError("Local cutout model is not configured")
import hashlib
if hashlib.md5(model.read_bytes()).hexdigest() != "fc16ebd8b0c10d971d3513d564d01e29":
    raise ValueError("Unexpected model checksum")
import onnxruntime as ort
pixels = np.asarray(photo.convert("RGB").resize((1024, 1024), Image.Resampling.LANCZOS)).astype(np.float32)
pixels = pixels / max(float(pixels.max()), 1e-6) - 0.5
options = ort.SessionOptions()
options.intra_op_num_threads = 2
options.inter_op_num_threads = 1
session = ort.InferenceSession(str(model), sess_options=options, providers=["CPUExecutionProvider"])
prediction = session.run(None, {session.get_inputs()[0].name: pixels.transpose(2, 0, 1)[None]})[0][0, 0]
prediction = (prediction - prediction.min()) / max(float(prediction.max() - prediction.min()), 1e-6)
mask = Image.fromarray((prediction * 255).astype(np.uint8)).resize(photo.size, Image.Resampling.LANCZOS)
alpha = np.asarray(mask).copy()
# Generic alpha cleanup only: never remove pixels based on this cat's fur/floor colours.
alpha[alpha < 10] = 0
alpha = np.minimum(alpha, np.asarray(photo.getchannel("A")))
mask = Image.fromarray(alpha)
bounds = mask.getbbox()
if not bounds:
    raise ValueError("No foreground was found")
photo.putalpha(mask)
photo = photo.crop(bounds)
photo.thumbnail((900, 900), Image.Resampling.LANCZOS)
out = Image.new("RGBA", (960, 960))
out.alpha_composite(photo, ((960 - photo.width) // 2, 930 - photo.height))
out.save(sys.stdout.buffer, format="PNG")
