"""Offline photo alpha extraction for the bundled desktop editor. No network calls."""
import hashlib
import io
import sys
import warnings
from pathlib import Path
from PIL import Image, ImageOps
import numpy as np
import onnxruntime as ort

MODEL_MD5 = "fc16ebd8b0c10d971d3513d564d01e29"

def main():
    Image.MAX_IMAGE_PIXELS = 40_000_000
    warnings.simplefilter("error", Image.DecompressionBombWarning)
    if len(sys.argv) != 2:
        raise ValueError("model path required")
    model = Path(sys.argv[1])
    if hashlib.md5(model.read_bytes()).hexdigest() != MODEL_MD5:
        raise ValueError("model checksum mismatch")
    raw = sys.stdin.buffer.read(30 * 1024 * 1024 + 1)
    if len(raw) > 30 * 1024 * 1024:
        raise ValueError("image too large")
    photo = ImageOps.exif_transpose(Image.open(io.BytesIO(raw))).convert("RGBA")
    if photo.width > 2000 or photo.height > 3000 or min(photo.size) < 16:
        raise ValueError("image dimensions out of bounds")
    # Keep exact input dimensions and RGB for the editor's aligned restoration brush.
    white = Image.new("RGB", photo.size, "white")
    white.paste(photo, mask=photo.getchannel("A"))
    pixels = np.asarray(white.resize((1024, 1024), Image.Resampling.LANCZOS)).astype(np.float32)
    pixels = pixels / max(float(pixels.max()), 1e-6) - 0.5
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    options.log_severity_level = 3
    session = ort.InferenceSession(str(model), sess_options=options, providers=["CPUExecutionProvider"])
    pred = session.run(None, {session.get_inputs()[0].name: pixels.transpose(2, 0, 1)[None]})[0][0, 0]
    pred = (pred - pred.min()) / max(float(pred.max() - pred.min()), 1e-6)
    mask = Image.fromarray((pred * 255).astype(np.uint8)).resize(photo.size, Image.Resampling.LANCZOS)
    alpha = np.minimum(np.asarray(mask), np.asarray(photo.getchannel("A"))).copy()
    alpha[alpha < 10] = 0
    alpha[alpha > 248] = 255
    if np.count_nonzero(alpha > 128) < 64:
        raise ValueError("no foreground found")
    photo.putalpha(Image.fromarray(alpha))
    photo.save(sys.stdout.buffer, format="PNG")

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # Never log photograph contents, filenames or user settings.
        print(type(error).__name__ + ": photo processing failed", file=sys.stderr)
        sys.exit(2)
