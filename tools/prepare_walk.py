"""Prepare the approved 4 x 2 green-screen walking atlas without per-frame resizing."""
from pathlib import Path
import json
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageOps
from scipy import ndimage

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1])
atlas = Image.open(source).convert("RGB")
width, height = atlas.size
frames, bounds = [], []
for index in range(8):
    column, row = index % 4, index // 4
    cell = atlas.crop((round(column * width / 4), round(row * height / 2),
                       round((column + 1) * width / 4), round((row + 1) * height / 2)))
    rgb = np.asarray(cell).astype(np.float32)
    maximum = np.maximum(rgb[:, :, 0], rgb[:, :, 2])
    spill = np.maximum(0, rgb[:, :, 1] - maximum)
    alpha = np.clip(1 - spill / np.maximum(rgb[:, :, 1], 1), 0, 1)
    alpha[spill < 24] = 1
    alpha[alpha < 0.06] = 0
    components, count = ndimage.label(alpha > 0.6)
    areas = np.bincount(components.ravel()); areas[0] = 0
    support = ndimage.binary_dilation(components == areas.argmax(), iterations=4)
    alpha[~support] = 0
    corrected = rgb / np.maximum(alpha[:, :, None], 0.01)
    edge = (alpha > 0) & (alpha < 1)
    corrected[:, :, 1][edge] = np.maximum(corrected[:, :, 0], corrected[:, :, 2])[edge]
    rgba = np.dstack((np.clip(corrected, 0, 255).astype(np.uint8), np.uint8(alpha * 255)))
    rgba[alpha == 0, :3] = 0
    frame = Image.fromarray(rgba)
    if not frame.getbbox():
        raise ValueError("Empty walking frame")
    frames.append(frame)
    bounds.append(frame.getbbox())

# One crop and scale for every frame keeps the torso and floor from pulsing.
common = tuple(min(box[i] for box in bounds) if i < 2 else max(box[i] for box in bounds)
               for i in range(4))
scale = 841 / (common[2] - common[0])
size = (841, round((common[3] - common[1]) * scale))
offset = (56, 1291 - size[1])
assets = root / "assets"
qa = root / "qa/walk-assets"
qa.mkdir(parents=True, exist_ok=True)
contact = Image.new("RGB", (1280, 640), "#273331")
draw = ImageDraw.Draw(contact)
preview = []
for index, frame in enumerate(frames):
    trimmed = frame.crop(common).resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (953, 1347))
    canvas.paste(trimmed, offset)
    pixels = np.array(canvas)
    pixels[pixels[:, :, 3] == 0, :3] = 0
    canvas = Image.fromarray(pixels)
    canvas.save(assets / ("cat-walk-" + str(index) + ".png"), optimize=True)
    shown = ImageOps.contain(canvas.crop((0, offset[1] - 15, 953, 1347)), (310, 290))
    position = (index % 4 * 320 + 5, index // 4 * 320 + 20)
    contact.paste(shown, position, shown)
    draw.text((position[0] + 5, position[1] - 14), str(index + 1), fill="white")
    background = Image.new("RGB", (400, 270), "#e6e7df")
    shown = ImageOps.contain(canvas.crop((0, offset[1] - 15, 953, 1347)), (400, 265))
    background.paste(shown, (0, 0), shown)
    preview.append(background)
contact.save(qa / "contact.png")
palette = preview[0].quantize(colors=256)
preview = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in preview]
preview[0].save(qa / "cycle.gif", save_all=True, append_images=preview[1:], duration=140,
                loop=0, optimize=False)
report = {"atlas_size": [width, height], "frame_bounds": bounds, "crop": common,
          "scale": scale, "offset": offset, "canvas": [953, 1347]}
(qa / "registration.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report))
