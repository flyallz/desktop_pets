import {chromium, expect} from '@playwright/test';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
import path from 'node:path';
import {parsePackage} from '../shared/pet-package.mjs';
const out=path.resolve('../.impeccable/review/looks');await mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});
const page=await context.newPage(),errors=[],checks=[];page.on('pageerror',e=>errors.push(e.message));
const menu=()=>page.getByRole('dialog',{name:'动作与造型',exact:true});
async function catPoint(){
 return page.locator('.pet-stage canvas').evaluate(c=>{
  const box=c.getBoundingClientRect(),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
  let best=null,distance=Infinity;
  for(let y=0;y<c.height;y+=6)for(let x=0;x<c.width;x+=6){if(data[(y*c.width+x)*4+3]<200)continue;const d=(x-c.width/2)**2+(y-c.height/2)**2;if(d<distance){distance=d;best={x:box.x+x*box.width/c.width,y:box.y+y*box.height/c.height};}}
  if(!best)throw Error('Pet did not render');return best;
 });
}
async function open(){const p=await catPoint();await page.mouse.click(p.x,p.y);await expect(menu()).toBeVisible();}
async function chooseLook(name){await open();await menu().getByRole('button',{name,exact:true}).click();await expect(menu()).toBeHidden();}
try{
 await page.goto('http://127.0.0.1:4317/',{waitUntil:'networkidle'});
 await page.getByRole('button',{name:'暂停',exact:true}).click();
 await open();await menu().getByRole('button',{name:'换个姿势',exact:true}).click();
 await expect(page.locator('.live-status')).toContainText('走一走');checks.push('click opens chooser and quick pose changes the active pose');
 await page.getByRole('button',{name:'原地陪伴 轻轻呼吸'}).click();
 await chooseLook('红围巾');
 await page.getByRole('button',{name:'暂停',exact:true}).click();
 await page.locator('.maker').screenshot({path:path.join(out,'scarf-desktop.png'),animations:'disabled'});
 await open();await menu().getByRole('button',{name:'换个造型',exact:true}).click();await expect(menu()).toBeHidden();
 await open();await expect(menu().getByRole('button',{name:'蓝领结',exact:true})).toHaveAttribute('aria-pressed','true');
 await page.screenshot({path:path.join(out,'menu-desktop.png'),fullPage:true,animations:'disabled'});checks.push('specific and quick outfit selection agree');
 await menu().getByRole('button',{name:'调整配饰位置'}).click();
 const fit=page.getByRole('dialog',{name:'调整配饰位置',exact:true});
 await fit.getByRole('slider',{name:'配饰左右'}).fill('53');
 await fit.getByRole('slider',{name:'配饰大小'}).fill('21');
 await fit.getByRole('button',{name:'完成调整',exact:true}).click();await expect(fit).toBeHidden();
 const download=page.waitForEvent('download');await page.getByRole('button',{name:'保存宠物包',exact:true}).click();
 const saved=await download;const filename=path.join(out,'styled.petpack.json');await saved.saveAs(filename);
 const pack=parsePackage(await readFile(filename,'utf8'));expect(pack.settings.appearance.style).toBe('bow');expect(pack.settings.appearance.anchors.idle.x).toBe(.53);expect(pack.settings.appearance.anchors.idle.width).toBe(.21);
 await page.locator('input[type=file]').nth(1).setInputFiles(filename);await expect(page.locator('.demo-tag')).toHaveText('我的桌宠');
 await open();await expect(menu().getByRole('button',{name:'蓝领结',exact:true})).toHaveAttribute('aria-pressed','true');await page.keyboard.press('Escape');await expect(menu()).toBeHidden();checks.push('adjusted appearance survives real export and reimport');
 const drag=await catPoint();await page.mouse.move(drag.x,drag.y);await page.mouse.down();await page.mouse.move(drag.x+60,drag.y-15,{steps:8});await page.mouse.up();await expect(menu()).toBeHidden();checks.push('dragging does not open the chooser or change appearance');
 await page.locator('.pet-stage canvas').focus();await page.keyboard.press('Enter');await expect(menu()).toBeVisible();await page.keyboard.press('Escape');await expect(menu()).toBeHidden();await expect(page.locator('.pet-stage canvas')).toBeFocused();checks.push('keyboard Enter and Escape open and dismiss controls');
 await chooseLook('红围巾');
 for(const [id,label] of [['idle','原地陪伴 轻轻呼吸'],['walk','走一走 8 帧动作'],['stretch','伸懒腰 舒展一下'],['sleep','睡一会儿 安静地睡']]){
  await page.getByRole('button',{name:label,exact:true}).click();await page.getByRole('button',{name:'暂停',exact:true}).click();
  await page.locator('.pet-stage').screenshot({path:path.join(out,'pose-'+id+'.png'),animations:'disabled'});
 }
 for(const width of [1024,390]){
  await page.setViewportSize({width,height:width===390?844:1000});await page.getByRole('button',{name:'原地陪伴 轻轻呼吸'}).click();await page.getByRole('button',{name:'暂停',exact:true}).click();
  await open();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:path.join(out,'menu-'+width+'.png'),fullPage:true,animations:'disabled'});
  await menu().getByRole('button',{name:'调整配饰位置'}).click();await page.locator('.pet-stage').screenshot({path:path.join(out,'fit-'+width+'.png'),animations:'disabled'});
  await fit.getByRole('button',{name:'取消',exact:true}).click();
 }
 checks.push('menu and placement controls fit narrow and wide viewports');
 await page.locator('input[type=file]').nth(0).setInputFiles(path.resolve('public/demo/cat.png'));
 await expect(page.getByRole('status').filter({hasText:'已换成你的照片'})).toBeVisible();
 await expect(page.getByRole('button',{name:'走一走 待添加图片'})).toBeVisible();
 await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
 await open();await expect(menu().getByRole('button',{name:'换个姿势',exact:true})).toBeDisabled();await expect(menu().getByRole('button',{name:'走一走',exact:true})).toBeDisabled();await menu().getByRole('button',{name:'红围巾',exact:true}).click();await expect(menu()).toBeHidden();checks.push('single-photo pet can wear accessories without borrowing demo actions');
 expect(errors).toEqual([]);await writeFile(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors}));
}finally{await context.close();await browser.close();}