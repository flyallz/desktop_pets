"""Developer-only: bundle the offline helper, model and dependency licenses."""
import argparse
import hashlib
import importlib.metadata
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request
import zipfile

parser=argparse.ArgumentParser()
parser.add_argument('--model',type=Path,required=True)
parser.add_argument('--skip-freeze',action='store_true')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
model=args.model.resolve()
if hashlib.md5(model.read_bytes()).hexdigest()!='fc16ebd8b0c10d971d3513d564d01e29':
    raise ValueError('Unexpected model checksum')
build=root/'.build'
bundle=build/'photo-tools-dist/PhotoCutout'
if not args.skip_freeze:
    subprocess.run([sys.executable,'-m','PyInstaller','--noconfirm','--onedir','--console','--noupx','--name','PhotoCutout',
        '--distpath',str(build/'photo-tools-dist'),'--workpath',str(build/'photo-tools-build'),'--specpath',str(build),
        '--collect-binaries','onnxruntime','--copy-metadata','onnxruntime','--exclude-module','tkinter',
        '--exclude-module','matplotlib','--exclude-module','pytest',str(root/'tools/photo_cutout.py')],check=True)
licenses=bundle/'licenses';licenses.mkdir(exist_ok=True)
for name in ['numpy','Pillow','onnxruntime','protobuf','flatbuffers','pyinstaller']:
    dist=importlib.metadata.distribution(name)
    for item in dist.files or []:
        if any(key in str(item).lower() for key in ['license','copying','notice']) and dist.locate_file(item).is_file():
            target=licenses/name/str(item).replace('..','_')
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(dist.locate_file(item),target)
python_license=Path(sys.base_prefix)/'LICENSE.txt'
shutil.copyfile(python_license,licenses/'Python-LICENSE.txt')
model_license=root/'docs/licenses/IS-Net-LICENSE.txt'
model_license.parent.mkdir(parents=True,exist_ok=True)
if not model_license.exists():
    request=urllib.request.Request('https://raw.githubusercontent.com/xuebinqin/DIS/main/LICENSE.md',headers={'User-Agent':'DesktopPets-build'})
    with urllib.request.urlopen(request,timeout=30) as response:
        model_license.write_bytes(response.read())
shutil.copyfile(model_license,licenses/'IS-Net-LICENSE.txt')
shutil.copyfile(root/'docs/photo-tools-notices.md',licenses/'README.md')
manifest=[]
output=build/'photo-tools.zip'
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for file in sorted(bundle.rglob('*')):
        if file.is_file():
            relative=file.relative_to(bundle).as_posix();z.write(file,relative)
            manifest.append(relative)
    z.write(model,'isnet-general-use.onnx');manifest.append('isnet-general-use.onnx')
(build/'photo-tools.sha256').write_text(hashlib.sha256(output.read_bytes()).hexdigest(),encoding='ascii')
print('Bundled',len(manifest),'files;',output.stat().st_size,'bytes')
