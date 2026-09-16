import http from 'node:http';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {existsSync} from 'node:fs';

const port = Number(process.env.PET_API_PORT || 4318);
const model = process.env.PET_MODEL_PATH;
let busy = false;
function reply(res, code, value) {
  res.writeHead(code, {'Content-Type':'application/json; charset=utf-8', 'Cache-Control':'no-store'});
  res.end(JSON.stringify(value));
}
const server = http.createServer(async (req, res) => {
  // Loopback helper for the beta maker; deliberately not a public upload service.
  const origin = req.headers.origin;
  if (origin && !['http://127.0.0.1:4317', 'http://localhost:4317','http://127.0.0.1:4319','http://localhost:4319'].includes(origin)) {
    return reply(res, 403, {error:'这个来源不能调用本机抠图。'});
  }
  if (!/^((127\.0\.0\.1)|(localhost)):\d+$/.test(req.headers.host || '')) return reply(res, 403, {error:'仅供本机调用。'});
  if (req.method === 'GET' && req.url === '/api/health') return reply(res, 200, {ready:!!model && existsSync(model), mode:'local', busy});
  if (req.method !== 'POST' || req.url !== '/api/cutout') return reply(res, 404, {error:'找不到这个功能。'});
  if (!['image/png','image/jpeg','image/webp'].includes((req.headers['content-type'] || '').split(';')[0])) return reply(res, 415, {error:'请选择 PNG、JPG 或 WebP 照片。'});
  if (!model || !existsSync(model)) return reply(res, 503, {error:'本机抠图尚未配置。可以先导入透明 PNG，或用画笔去掉背景。'});
  if (busy) return reply(res, 429, {error:'正在处理另一张照片，请稍后再试。'});
  busy = true;
  let total = 0;
  const chunks = [];
  try {
    for await (const chunk of req) {
      total += chunk.length;
      if (total > 10 * 1024 * 1024) { reply(res, 413, {error:'照片需小于 10 MB。'}); busy = false; return; }
      chunks.push(chunk);
    }
  } catch { busy = false; return; }
  const worker = spawn(process.env.PET_PYTHON || 'python', [fileURLToPath(new URL('./cutout.py', import.meta.url))], {
    windowsHide:true, stdio:['pipe','pipe','pipe'], env:process.env,
  });
  let finished = false;
  let resultSize = 0;
  const output = [];
  const complete = (code, message) => {
    if (finished) return;
    finished = true;
    busy = false;
    clearTimeout(timer);
    if (!res.destroyed) {
      if (code === 200) {
        res.writeHead(200, {'Content-Type':'image/png','Cache-Control':'no-store'});
        res.end(Buffer.concat(output));
      } else reply(res, code, {error:message});
    }
  };
  const timer = setTimeout(() => { worker.kill(); complete(504, '这张照片处理超时，可以换一张背景更清楚的照片。'); }, 60_000);
  worker.stdout.on('data', chunk => {
    resultSize += chunk.length;
    if (resultSize > 8 * 1024 * 1024) { worker.kill(); complete(500, '处理结果过大。'); }
    else output.push(chunk);
  });
  worker.stderr.resume(); // Do not log photo bytes or user paths.
  worker.stdin.on('error', () => {});
  worker.on('error', () => complete(503, '本机抠图环境未准备好，请检查 Python 和模型配置。'));
  worker.on('close', code => complete(code === 0 && resultSize > 0 ? 200 : 422, '未能识别这张照片，请尝试更清晰的 JPG 或 PNG。'));
  res.on('close', () => { if (!finished) { worker.kill(); complete(499, '请求已结束。'); } });
  worker.stdin.end(Buffer.concat(chunks));
});
server.requestTimeout = 65_000;
server.headersTimeout = 10_000;
server.listen(port, '127.0.0.1', () => console.log('Local cutout helper: http://127.0.0.1:' + port));
