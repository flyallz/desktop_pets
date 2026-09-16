export async function loadImage(source) {
  const img = new Image();
  img.src = source;
  await img.decode();
  return img;
}
export async function imageToAsset(blob, id = 'photo') {
  if (blob.size > 10 * 1024 * 1024) throw new Error('照片需小于 10 MB。');
  if (!['image/png','image/jpeg','image/webp'].includes(blob.type)) throw new Error('请选择 JPG、PNG 或 WebP 图片。');
  const url = URL.createObjectURL(blob);
  try {
    const image = await loadImage(url);
    if (image.width * image.height > 20_000_000) throw new Error('照片像素过高，请先缩小到 2000 万像素以内。');
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 960;
    const context = canvas.getContext('2d', {willReadFrequently:true});
    const scale = Math.min(900 / image.width, 900 / image.height, 1);
    const w = Math.round(image.width * scale), h = Math.round(image.height * scale);
    context.drawImage(image, (960 - w) / 2, 930 - h, w, h);
    return {id, width:960, height:960, data:canvas.toDataURL('image/png')};
  } finally { URL.revokeObjectURL(url); }
}
export async function loadDemo() {
  const response = await fetch('./demo/pet.json');
  if (!response.ok) throw new Error('示例宠物读取失败，请刷新重试。');
  const demo = await response.json();
  for (const asset of demo.assets) {
    const response = await fetch('./demo/' + asset.file);
    if (!response.ok) throw new Error('示例图片读取失败。');
    const blob = await response.blob();
    asset.data = await new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result);
      reader.onerror = reject;
      reader.readAsDataURL(blob);
    });
    delete asset.file;
  }
  return demo;
}
export function saveDownload(text, filename) {
  const blob = new Blob([text], {type:'application/json'});
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}
