import {chromium, expect} from '@playwright/test';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import path from 'node:path';
import {parsePackage} from '../shared/pet-package.mjs';
const out=path.resolve('../.impeccable/review');
await mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true,deviceScaleFactor:1});
const page=await context.newPage();
const errors=[];page.on('pageerror',error=>errors.push(error.message));
const evidence={checks:[],viewports:[]};
async function check(name,fn){await fn();evidence.checks.push(name);}
try{
 await page.goto('http://127.0.0.1:4317/',{waitUntil:'networkidle'});
 await expect(page.getByRole('heading',{name:'把它带到你的桌面。'})).toBeVisible();
 await expect(page.getByRole('button',{name:'自动去背景'})).toBeEnabled();
 await check('eight-frame sample walk can be played',async()=>{
  await page.getByRole('button',{name:'走一走 8 帧动作'}).click();
  await expect(page.locator('.live-status')).toContainText('走一走');
 });
 await check('manual sleep and explicit pause',async()=>{
  await page.getByRole('button',{name:'睡一会儿 点击叫醒'}).click();
  await expect(page.locator('.live-status')).toContainText('睡一会儿');
  await page.getByRole('button',{name:'暂停',exact:true}).click();
  await expect(page.locator('.live-status')).toContainText('已暂停');
  await page.getByRole('button',{name:'继续',exact:true}).click();
 });
 await page.getByRole('button',{name:'原地陪伴 轻轻呼吸'}).click();
 await page.getByRole('button',{name:'暂停',exact:true}).click();
 await expect(page.locator('.live-status')).toContainText('已暂停');
 await page.screenshot({path:path.join(out,'desktop.png'),fullPage:true,animations:'disabled'});
 for(const width of [1440,1024,390]){
  await page.setViewportSize({width,height:width===390?844:1000});
  await page.evaluate(()=>window.scrollTo(0,0));
  const dimensions=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,viewport:innerWidth}));
  expect(dimensions.scroll).toBeLessThanOrEqual(width);
  evidence.viewports.push({width,...dimensions});
  if(width===390)await page.screenshot({path:path.join(out,'mobile.png'),fullPage:true,animations:'disabled'});
  if(width===1024)await page.screenshot({path:path.join(out,'user-1024.png'),fullPage:true,animations:'disabled'});
 }
 await page.setViewportSize({width:1440,height:1000});
 await check('upload replaces sample poses with the user photo',async()=>{
  await page.locator('input[type=file]').nth(0).setInputFiles(path.resolve('public/demo/cat.png'));
  await expect(page.locator('.demo-tag')).toHaveText('我的桌宠');
  await expect(page.getByRole('button',{name:'走一走 待添加图片'})).toBeVisible();
  await expect(page.getByRole('button',{name:'睡一会儿 待添加图片'})).toBeVisible();
 });
 await page.getByLabel('它叫什么？').fill('测试猫咪');
 await check('erase then undo restores original photo',async()=>{
  await page.getByRole('button',{name:'画笔修整'}).click();
  const c=page.getByLabel('背景擦除画布'),before=await c.evaluate(el=>el.toDataURL());
  await c.click({position:{x:130,y:130}});
  expect(await c.evaluate(el=>el.toDataURL())).not.toEqual(before);
  await page.getByRole('button',{name:'撤销',exact:true}).click();
  expect(await c.evaluate(el=>el.toDataURL())).toEqual(before);
  await page.getByRole('button',{name:'保存修整'}).click();
 });
 await check('single photo is rejected as a walking animation',async()=>{
  await page.getByRole('button',{name:'2 动作',exact:true}).click();
  await page.locator('input[type=file]').nth(2).setInputFiles(path.resolve('public/demo/cat.png'));
  await expect(page.getByRole('alert')).toContainText('至少 4 张');
  await page.getByRole('button',{name:'关闭错误提示'}).click();
 });
 await check('custom multi-frame walk can be added',async()=>{
  await page.locator('input[type=file]').nth(2).setInputFiles([0,1,2,3].map(i=>path.resolve('public/demo/cat-walk-'+i+'.png')));
  await expect(page.getByRole('button',{name:'走一走 4 帧动作'})).toBeVisible();
 });
 await page.getByRole('switch',{name:'自行切换动作'}).uncheck();
 await page.getByRole('button',{name:'3 陪伴',exact:true}).click();
 await page.getByRole('switch',{name:'休息提醒'}).check();
 await page.getByLabel('每隔多久').selectOption('30');
 await page.getByLabel('提醒时说一句').fill('起来伸个懒腰吧');
 await page.getByRole('button',{name:'开始 25 分钟专注',exact:true}).click();
 await expect(page.getByRole('button',{name:/结束专注/})).toBeVisible();
 let downloadPath;
 await check('export then import preserves user settings and images',async()=>{
  const downloadEvent=page.waitForEvent('download');
  await page.getByRole('button',{name:'保存宠物包',exact:true}).click();
  const download=await downloadEvent;
  downloadPath=path.join(out,'test.petpack.json');await download.saveAs(downloadPath);
  const pet=parsePackage(await readFile(downloadPath,'utf8'));
  expect(pet.pet.name).toBe('测试猫咪');expect(pet.actions.walk.frames).toHaveLength(4);
  expect(pet.settings.autoPlay).toBe(false);expect(pet.settings.reminder.minutes).toBe(30);
  await page.locator('input[type=file]').nth(1).setInputFiles(downloadPath);
  await expect(page.getByLabel('它叫什么？')).toHaveValue('测试猫咪');
 });
 await check('invalid file leaves existing work intact',async()=>{
  await page.locator('input[type=file]').nth(1).setInputFiles({name:'bad.petpack.json',mimeType:'application/json',buffer:Buffer.from('bad json')});
  await expect(page.getByRole('alert')).toContainText('无法读取');
  await expect(page.getByLabel('它叫什么？')).toHaveValue('测试猫咪');
 });
 expect(errors).toEqual([]);
 evidence.consoleErrors=errors;
 await writeFile(path.join(out,'ui-checks.json'),JSON.stringify(evidence,null,2));
 console.log(JSON.stringify(evidence,null,2));
}finally{await browser.close();}
