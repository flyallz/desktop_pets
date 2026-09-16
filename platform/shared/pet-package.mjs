// Data-only format shared by the website and desktop application.
export const FORMAT = 'desktop-pets';
export const VERSION = 1;
export const MAX_PACKAGE_BYTES = 32 * 1024 * 1024;
export const ACTIONS = ['idle', 'walk', 'stretch', 'sleep'];
export const DEFAULT_SETTINGS = Object.freeze({
  autoPlay: true, activity: 'calm', size: 260,
  reminder: { enabled: false, minutes: 45, message: '起来活动一下，也喝口水吧。' },
});
function fail(message) { throw new Error(message); }
function object(value) { return !!value && typeof value === 'object' && !Array.isArray(value); }
function text(value, max, label) {
  if (typeof value !== 'string' || !value.trim() || value.trim().length > max || /[\u0000-\u001f]/.test(value)) fail(label + '不正确。');
  return value.trim();
}
function int(value, min, max, label) {
  if (!Number.isInteger(value) || value < min || value > max) fail(label + '超出范围。');
  return value;
}
function pngSize(data) {
  if (typeof data !== 'string' || data.length > 6 * 1024 * 1024 || !/^data:image\/png;base64,[A-Za-z0-9+/]+={0,2}$/.test(data)) fail('图片必须是内嵌 PNG，不能包含网址或脚本。');
  const value = data.slice(22);
  if (value.length % 4 !== 0) fail('图片编码不完整。');
  let binary;
  try { binary = atob(value.slice(0, 44)); } catch { fail('图片编码无法读取。'); }
  if (binary.slice(0, 8) !== '\x89PNG\r\n\x1a\n' || binary.slice(12, 16) !== 'IHDR') fail('图片内容不是有效的 PNG。');
  const read = i => ((binary.charCodeAt(i) * 2 ** 24) + (binary.charCodeAt(i + 1) << 16) + (binary.charCodeAt(i + 2) << 8) + binary.charCodeAt(i + 3));
  return [read(16), read(20)];
}
export function validatePackage(input) {
  if (!object(input) || input.format !== FORMAT) fail('这不是桌宠工坊的宠物包。');
  if (input.version !== VERSION) fail('暂不支持这个宠物包版本，请使用对应版本的客户端。');
  if (!object(input.pet)) fail('缺少宠物信息。');
  const name = text(input.pet.name, 30, '宠物名字');
  if (!Array.isArray(input.assets) || input.assets.length < 1 || input.assets.length > 32) fail('宠物包需要 1～32 张图片。');
  let total = 0, pixels = 0;
  const ids = new Set();
  const assets = input.assets.map(asset => {
    if (!object(asset) || typeof asset.id !== 'string' || !/^[a-z0-9_-]{1,40}$/.test(asset.id) || ids.has(asset.id)) fail('图片编号无效或重复。');
    ids.add(asset.id);
    const width = int(asset.width, 1, 2048, '图片宽度');
    const height = int(asset.height, 1, 2048, '图片高度');
    pixels += width * height;
    if(pixels > 32_000_000) fail('图片总像素过高，请缩小动作图片。');
    const actual = pngSize(asset.data);
    if (actual[0] !== width || actual[1] !== height) fail('图片尺寸与记录不一致。');
    total += asset.data.length;
    if (total > MAX_PACKAGE_BYTES) fail('宠物包超过 32 MB，请减少图片或缩小尺寸。');
    return { id: asset.id, width, height, data: asset.data };
  });
  if (!object(input.actions) || !input.actions.idle) fail('宠物包必须有原地陪伴姿态。');
  const actions = {};
  for (const id of ACTIONS) {
    const action = input.actions[id];
    if (action === undefined) continue;
    if (!object(action) || !Array.isArray(action.frames) || action.frames.length < 1 || action.frames.length > 24) fail('动作图片数量不正确。');
    if (id === 'walk' && action.frames.length < 4) fail('行走至少需要 4 帧，单张图片不能当作完整步态。');
    const frames = action.frames.map(frame => {
      if (typeof frame !== 'string' || !ids.has(frame)) fail('动作引用了不存在的图片。');
      return frame;
    });
    const sizes = frames.map(frame => assets.find(asset => asset.id === frame));
    if (sizes.some(asset => asset.width !== sizes[0].width || asset.height !== sizes[0].height)) fail('同一动作的帧需要使用相同画布尺寸。');
    actions[id] = { frames, fps: int(action.fps, 1, 24, '动作帧率') };
  }
  const settings = input.settings;
  if (!object(settings) || typeof settings.autoPlay !== 'boolean' || !['calm','lively'].includes(settings.activity)) fail('自动活动设置不正确。');
  const reminder = settings.reminder;
  if (!object(reminder) || typeof reminder.enabled !== 'boolean') fail('陪伴提醒设置不正确。');
  return {
    format: FORMAT, version: VERSION, pet: { name }, assets, actions,
    settings: {
      autoPlay: settings.autoPlay, activity: settings.activity,
      size: int(settings.size, 160, 360, '桌宠大小'),
      reminder: {
        enabled: reminder.enabled, minutes: int(reminder.minutes, 5, 180, '提醒间隔'),
        message: text(reminder.message, 80, '提醒内容'),
      },
    },
  };
}
export function parsePackage(textValue) {
  if (typeof textValue !== 'string' || new TextEncoder().encode(textValue).length > MAX_PACKAGE_BYTES) fail('文件超过 32 MB。');
  let data;
  try { data = JSON.parse(textValue); } catch { fail('宠物包文件无法读取，请选择导出的 .petpack.json 文件。'); }
  return validatePackage(data);
}
export function serializePackage(value) {
  const result = JSON.stringify(validatePackage(value));
  if (new TextEncoder().encode(result).length > MAX_PACKAGE_BYTES) fail('宠物包超过 32 MB。');
  return result;
}
export function newPackage(name, asset) {
  return validatePackage({format: FORMAT, version: VERSION, pet: {name}, assets: [asset],
    actions: {idle: {frames: [asset.id], fps: 1}},
    settings: {...DEFAULT_SETTINGS, reminder: {...DEFAULT_SETTINGS.reminder}}});
}
export function pruneAssets(pet) {
  const used = new Set(Object.values(pet.actions).flatMap(action => action.frames));
  return {...pet, assets: pet.assets.filter(asset => used.has(asset.id))};
}
