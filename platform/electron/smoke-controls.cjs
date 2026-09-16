const fs=require('node:fs/promises');
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function waitFor(win,script){
  for(let i=0;i<60;i++){if(await win.webContents.executeJavaScript(script))return true;await pause(100);}
  return false;
}
async function tapCat(win){
  const point=await win.webContents.executeJavaScript(`(()=>{
    const c=document.querySelector('canvas'),box=c.getBoundingClientRect(),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
    let point=null,distance=Infinity;
    for(let y=0;y<c.height;y+=6)for(let x=0;x<c.width;x+=6){if(data[(y*c.width+x)*4+3]<200)continue;const d=(x-c.width/2)**2+(y-c.height/2)**2;if(d<distance){distance=d;point={x:Math.round(box.x+x*box.width/c.width),y:Math.round(box.y+y*box.height/c.height)};}}
    return point;
  })()`);
  if(!point)return false;
  win.webContents.sendInputEvent({type:'mouseMove',...point});
  win.webContents.sendInputEvent({type:'mouseDown',button:'left',clickCount:1,...point});
  win.webContents.sendInputEvent({type:'mouseUp',button:'left',clickCount:1,...point});
  return waitFor(win,"!!document.querySelector('.pet-menu')");
}
module.exports=async function testControls(win,storage){
  win.focus();await pause(100);
  const menuUsable=await tapCat(win);
  if(!menuUsable)return {menuUsable:false,appearanceSaved:false,appearanceRestored:false};
  await win.webContents.executeJavaScript("[...document.querySelectorAll('.pet-look-options button')].find(b=>b.textContent.includes('蓝领结')).click()");
  await waitFor(win,"!document.querySelector('.pet-menu')");
  let appearanceSaved=false;
  try{appearanceSaved=JSON.parse(await fs.readFile(storage,'utf8')).settings.appearance.style==='bow';}catch{}
  await new Promise(resolve=>{win.webContents.once('did-finish-load',resolve);win.webContents.reload();});
  await waitFor(win,"(()=>{const c=document.querySelector('canvas');return !!c&&Array.from(c.getContext('2d').getImageData(0,0,c.width,c.height).data).some((v,i)=>i%4===3&&v>24)})()");
  const reopened=await tapCat(win);
  const appearanceRestored=reopened&&await win.webContents.executeJavaScript("[...document.querySelectorAll('.pet-look-options button')].some(b=>b.textContent.includes('蓝领结')&&b.getAttribute('aria-pressed')==='true')");
  if(reopened)await win.webContents.executeJavaScript("document.querySelector('.pet-menu-close').click()");
  await pause(100);
  return {menuUsable,appearanceSaved,appearanceRestored};
};