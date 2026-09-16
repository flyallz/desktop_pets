"""Make transparent idle looks from the owner's original photos, entirely locally.
Requires the same IS-Net runtime/model as make_cutout.py. Raw photos and review
sheets remain in ignored local folders; only transparent cutouts are distributable.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--photos', type=Path, required=True)
parser.add_argument('--runtime', type=Path, required=True)
parser.add_argument('--output', type=Path, default=Path('assets/looks'))
parser.add_argument('--review', type=Path, default=Path('.build/photo-looks'))
args = parser.parse_args()
sys.path.insert(0, str(args.runtime / 'deps'))
import numpy as np
import onnxruntime as ort
from scipy import ndimage
from PIL import Image, ImageOps, ImageDraw, ImageFilter

model = args.runtime / 'models/isnet-general-use.onnx'
assert hashlib.md5(model.read_bytes()).hexdigest() == 'fc16ebd8b0c10d971d3513d564d01e29'
options = ort.SessionOptions()
options.intra_op_num_threads = 4
options.inter_op_num_threads = 1
session = ort.InferenceSession(str(model), sess_options=options, providers=['CPUExecutionProvider'])
looks = [
    ('sweater', '红毛衣', 'fbaa070639d2a6ab9746e73dd316b558.jpg', None),
    ('tilt', '歪头看你', 'b002f60e36fce5bae37b0138b74560f8.jpg', None),
    ('sofa', '乖乖侧坐', '925710e5a860a1445fd7c86a4eff42e1.jpg', (100, 325, 720, 850)),
    ('belly', '露肚皮睡觉', 'e671cf6626cdf65c51ad2220b17f9e3f.jpg', (240, 280, 960, 1280)),
    ('back', '毛茸茸的背影', 'a734a7e3d5805f082b1d99a078719914.jpg', None),
]
args.output.mkdir(parents=True, exist_ok=True)
args.review.mkdir(parents=True, exist_ok=True)
report = []
for ident, label, filename, region in looks:
    source_path = args.photos / filename
    original = ImageOps.exif_transpose(Image.open(source_path)).convert('RGB')
    photo = original.crop(region) if region else original
    rgb = np.asarray(photo.resize((1024, 1024), Image.Resampling.LANCZOS)).astype(np.float32)
    inputs = (rgb / max(float(rgb.max()), 1e-6) - 0.5).transpose(2, 0, 1)[None]
    predicted = session.run(None, {session.get_inputs()[0].name: inputs})[0][0, 0]
    predicted = (predicted - predicted.min()) / max(float(predicted.max() - predicted.min()), 1e-6)
    mask = Image.fromarray((predicted * 255).astype(np.uint8)).resize(photo.size, Image.Resampling.LANCZOS)
    alpha = np.array(mask)
    alpha[alpha < 24] = 0
    alpha[alpha > 248] = 255
    if ident == 'sweater':
        # The white toy behind the ears touches the fur in this photo. Restrict
        # the background above the original ear/head contour, retaining RGB.
        permitted = Image.new('L', photo.size)
        ImageDraw.Draw(permitted).polygon([(0,590),(325,590),(343,430),(366,306),
            (383,299),(453,346),(537,342),(629,353),(719,301),(736,307),
            (745,365),(736,429),(740,514),(729,591),(960,591),(960,1282),(0,1282)], fill=255)
        alpha = np.minimum(alpha, np.asarray(permitted))
        # Restore the actual sweater-covered left haunch: the red bedding
        # confused the general-purpose segmenter. These points follow the
        # original cat's outline, and never replace any source colors.
        body = Image.new('L', photo.size)
        ImageDraw.Draw(body).polygon([(358,592),(337,615),(326,666),(299,680),
            (278,710),(256,749),(232,781),(220,819),(217,863),(222,906),
            (238,942),(267,965),(314,987),(352,996),(390,960),(415,874),
            (428,755),(397,648)], fill=255)
        alpha = np.maximum(alpha, np.asarray(body.filter(ImageFilter.GaussianBlur(0.8))))
        # Preserve the pale toe at the bottom of the front cuff.
        paw = Image.new('L', photo.size)
        ImageDraw.Draw(paw).ellipse((399,1127,536,1209), fill=255)
        alpha = np.maximum(alpha, np.asarray(paw.filter(ImageFilter.GaussianBlur(0.9))))
    if ident in ('back', 'desk'):
        color = np.asarray(photo).astype(np.int16)
        floor = (alpha < 225) & (color[:, :, 0] > color[:, :, 1] + 16) & (color[:, :, 0] > color[:, :, 2] + 32)
        alpha[floor] = 0
    components, count = ndimage.label(alpha >= 128)
    if not count:
        raise RuntimeError('No foreground: ' + ident)
    areas = np.bincount(components.ravel()); areas[0] = 0
    alpha[~ndimage.binary_dilation(components == areas.argmax(), iterations=5)] = 0
    mask = Image.fromarray(alpha)
    mask.save(args.review / (ident + '-mask.png'))
    cutout = photo.convert('RGBA'); cutout.putalpha(mask)
    cutout = cutout.crop(mask.getbbox())
    cutout = ImageOps.contain(cutout, (857, 1235), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (953, 1347))
    canvas.alpha_composite(cutout, ((953-cutout.width)//2, 1307-cutout.height))
    pixels = np.array(canvas)
    pixels[pixels[:, :, 3] == 0, :3] = 0
    canvas = Image.fromarray(pixels)
    canvas.save(args.output / (ident + '.png'), optimize=True)
    panel = Image.new('RGB', (900, 480), '#f8f3eb')
    for col, background in enumerate(('#f8f3eb', '#292d34', '#f8f3eb')):
        tile = Image.new('RGBA', (300, 460), background)
        shown = ImageOps.contain(photo if col == 0 else canvas, (280, 430))
        if col == 0:
            tile.paste(shown, ((300-shown.width)//2, (460-shown.height)//2))
        else:
            tile.alpha_composite(shown, ((300-shown.width)//2, (460-shown.height)//2))
        panel.paste(tile.convert('RGB'), (300*col, 20))
    ImageDraw.Draw(panel).text((12, 4), ident, fill='black')
    panel.save(args.review / (ident + '.jpg'), quality=90)
    report.append({'id': ident, 'label': label, 'source': filename,
        'source_sha256': hashlib.sha256(source_path.read_bytes()).hexdigest(),
        'crop': region, 'processing': 'Local IS-Net alpha extraction; original photo colors; canvas normalization only',
        'canvas': [953,1347]})
    print(ident + ': ready', flush=True)
(args.review / 'provenance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
