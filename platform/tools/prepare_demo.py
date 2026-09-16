"""Create web-size copies of the already approved public cat assets."""
from pathlib import Path
from PIL import Image, ImageOps, PngImagePlugin
import json
root=Path(__file__).resolve().parents[2]
output=root/"platform/public/demo"
output.mkdir(parents=True,exist_ok=True)
names=["cat","cat-stretch","cat-sleep"]+[f"cat-walk-{i}" for i in range(8)]
assets=[]
for name in names:
    source=Image.open(root/"assets"/(name+".png")).convert("RGBA")
    source.thumbnail((900,900),Image.Resampling.LANCZOS)
    canvas=Image.new("RGBA",(960,960))
    canvas.alpha_composite(source,((960-source.width)//2,930-source.height))
    meta=PngImagePlugin.PngInfo()
    meta.add_text("impeccable:prompt","User-approved public asset: assets/"+name+".png; resized onto shared 960px canvas. See docs/pose-assets.md for generation and source provenance.")
    canvas.save(output/(name+".png"),pnginfo=meta,optimize=True)
    assets.append({"id":name,"width":960,"height":960,"file":name+".png"})
meta=PngImagePlugin.PngInfo()
meta.add_text("impeccable:prompt","User-approved original cutout assets/cat.png; reduced for tray icon.")
ImageOps.contain(Image.open(root/"assets/cat.png"),(64,64)).save(output/"tray.png",pnginfo=meta)
pet={"format":"desktop-pets","version":1,"pet":{"name":"朋友的猫"},"assets":assets,"actions":{
"idle":{"frames":["cat"],"fps":1},"walk":{"frames":[f"cat-walk-{i}" for i in range(8)],"fps":8},
"stretch":{"frames":["cat-stretch"],"fps":1},"sleep":{"frames":["cat-sleep"],"fps":1}},
"settings":{"autoPlay":True,"activity":"calm","size":260,"reminder":{"enabled":False,"minutes":45,"message":"起来活动一下，也喝口水吧。"}}}
pet["settings"]["appearance"]={'style': 'none', 'anchors': {'idle': {'x': 0.515, 'y': 0.33, 'width': 0.23, 'angle': 5}, 'walk': {'x': 0.69, 'y': 0.755, 'width': 0.135, 'angle': -12}, 'stretch': {'x': 0.625, 'y': 0.8, 'width': 0.165, 'angle': 8}, 'sleep': {'x': 0.635, 'y': 0.872, 'width': 0.135, 'angle': 6}}}
(output/"pet.json").write_text(json.dumps(pet,ensure_ascii=False,indent=2),encoding="utf-8")
print("Prepared",len(assets),"approved demo poses")
