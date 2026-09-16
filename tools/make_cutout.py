"""Extract the user's cat locally; preserve source RGB, use IS-Net only for alpha."""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".build" / "deps"))
import numpy as np
import onnxruntime as ort
from scipy import ndimage
from PIL import Image, ImageOps

source = Path(sys.argv[1])
model = ROOT / ".build" / "models" / "isnet-general-use.onnx"
expected = "fc16ebd8b0c10d971d3513d564d01e29"
if hashlib.md5(model.read_bytes()).hexdigest() != expected:
    raise RuntimeError("Model checksum does not match upstream rembg release")

photo = ImageOps.exif_transpose(Image.open(source)).convert("RGB")
rgb = np.asarray(photo.resize((1024, 1024), Image.Resampling.LANCZOS)).astype(np.float32)
normalized = rgb / max(float(rgb.max()), 1e-6) - 0.5
options = ort.SessionOptions()
options.intra_op_num_threads = 4
options.inter_op_num_threads = 1
session = ort.InferenceSession(str(model), sess_options=options, providers=["CPUExecutionProvider"])
prediction = session.run(None, {session.get_inputs()[0].name: normalized.transpose(2, 0, 1)[None]})[0][0, 0]
prediction = (prediction - prediction.min()) / max(float(prediction.max() - prediction.min()), 1e-6)
mask = Image.fromarray((prediction * 255).astype(np.uint8)).resize(photo.size, Image.Resampling.LANCZOS)
alpha = np.asarray(mask).copy()
# Remove reddish wooden-floor spill only in uncertain pixels below the body.
original_rgb = np.asarray(photo).astype(np.int16)
y_coordinates = np.arange(photo.height)[:, None]
floor_spill = ((y_coordinates > photo.height * 0.50) & (alpha < 225)
               & (original_rgb[:, :, 0] > original_rgb[:, :, 1] + 16)
               & (original_rgb[:, :, 0] > original_rgb[:, :, 2] + 32))
alpha[floor_spill] = 0
alpha[alpha < 20] = 0
alpha[alpha > 248] = 255
# Keep soft fur near the confident, connected cat; discard detached floor residue.
components, count = ndimage.label(alpha >= 128)
if count == 0:
    raise RuntimeError("No confident cat foreground")
areas = np.bincount(components.ravel())
areas[0] = 0
main_cat = components == areas.argmax()
soft_support = ndimage.binary_dilation(main_cat, iterations=6)
alpha[~soft_support] = 0
mask = Image.fromarray(alpha)
bounds = mask.getbbox()
if not bounds:
    raise RuntimeError("Segmentation returned an empty foreground")
cutout = photo.convert("RGBA")
cutout.putalpha(mask)
cutout = cutout.crop(bounds)
padding = round(max(cutout.size) * 0.045)
padded = Image.new("RGBA", (cutout.width + padding * 2, cutout.height + padding * 2))
padded.paste(cutout, (padding, padding))
assets = ROOT / "assets"
qa = ROOT / "qa"
assets.mkdir(exist_ok=True)
qa.mkdir(exist_ok=True)
padded.save(assets / "cat.png", optimize=True)
icon = Image.new("RGBA", (256, 256))
icon_image = ImageOps.contain(padded, (256, 256))
icon.alpha_composite(icon_image, ((256 - icon_image.width) // 2, (256 - icon_image.height) // 2))
icon.save(assets / "cat.ico", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
mask.save(qa / "source-mask.png")

# Review soft fur edges on both dark and light backgrounds, beside the source.
panel = (360, 540)
contact = Image.new("RGB", (panel[0] * 3, panel[1]), "#edeae4")
original = ImageOps.contain(photo, panel)
contact.paste(original, ((panel[0] - original.width) // 2, (panel[1] - original.height) // 2))
for i, color in enumerate(("#292d34", "#faf7ef"), 1):
    background = Image.new("RGBA", panel, color)
    shown = ImageOps.contain(padded, (panel[0] - 24, panel[1] - 24))
    background.alpha_composite(shown, ((panel[0] - shown.width) // 2, (panel[1] - shown.height) // 2))
    contact.paste(background.convert("RGB"), (i * panel[0], 0))
contact.save(qa / "cutout-review.jpg", quality=88)
contact.resize((840, 420)).save(qa / "cutout-review-small.jpg", quality=78)
report = {
    "source_name": source.name,
    "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "source_size": photo.size,
    "source_crop": bounds,
    "output_size": padded.size,
    "processing": "local IS-Net alpha extraction with floor-spill cleanup; original RGB preserved, no image generation",
    "model_md5": expected,
    "model_source": "https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx",
    "preprocessing_reference": "https://github.com/danielgatis/rembg/blob/main/rembg/sessions/dis_general_use.py",
}
(qa / "asset-provenance.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
