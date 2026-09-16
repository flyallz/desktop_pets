const {app,BrowserWindow,ipcMain,Menu,Tray,nativeImage,dialog,screen,Notification,shell,session} = require('electron');
const fs=require('node:fs/promises'),path=require('node:path');
let petWindow,studioWindow,tray,savedText=null,autoPlay=true,dragTimer=null,dragOrigin=null,parsePackage,clampPosition,lastMove=0,lastNotify=0;
const smoke=process.argv.includes('--smoke-test');
if(smoke)app.setPath('userData',path.resolve('../.build/electron-smoke-profile'));
const outIndex=path.join(__dirname,'../dist/index.html');
const storage=()=>path.join(app.getPath('userData'),'pet.json');
const safePreferences={preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true,webSecurity:true};
function allowed(event){return [petWindow,studioWindow].some(w=>w&&!w.isDestroyed()&&w.webContents===event.sender)&&event.senderFrame===event.sender.mainFrame;}
function petOnly(event){return allowed(event)&&event.sender===petWindow.webContents;}
function send(action,extra={}){if(petWindow&&!petWindow.isDestroyed())petWindow.webContents.send('pet:command',{action,...extra});}
function secure(win){
  win.webContents.setWindowOpenHandler(({url})=>{
    if(['https://github.com/flyallz/desktop_pets/releases','https://github.com/flyallz/desktop_pets/actions/workflows/platform-build.yml'].includes(url))shell.openExternal(url);
    return {action:'deny'};
  });
  win.webContents.on('will-navigate',(event,url)=>{if(!url.startsWith('file://'))event.preventDefault();});
}
function showPet(){petWindow.showInactive();petWindow.setAlwaysOnTop(true,process.platform==='win32'?'screen-saver':'floating');}
function recenter(){
  const area=screen.getDisplayNearestPoint(screen.getCursorScreenPoint()).workArea;
  const [w,h]=petWindow.getSize();
  petWindow.setPosition(area.x+Math.max(0,area.width-w-20),area.y+Math.max(0,area.height-h),false);
}
function maker(){
  if(studioWindow&&!studioWindow.isDestroyed()){studioWindow.show();studioWindow.focus();return;}
  studioWindow=new BrowserWindow({width:1320,height:930,minWidth:390,minHeight:620,title:'桌宠工坊',backgroundColor:'#f4f6f6',autoHideMenuBar:true,webPreferences:safePreferences});
  secure(studioWindow);studioWindow.loadFile(outIndex,{hash:'studio'});
}
async function verify(text){
  const value=parsePackage(text);
  for(const asset of value.assets){
    const image=nativeImage.createFromDataURL(asset.data);
    const size=image.getSize();
    if(image.isEmpty()||size.width!==asset.width||size.height!==asset.height)throw new Error('宠物包中有损坏的图片。');
  }
  return value;
}
async function importPet(){
  const result=await dialog.showOpenDialog({title:'导入宠物包',properties:['openFile'],filters:[{name:'桌宠宠物包',extensions:['json','petpack']}]});
  if(result.canceled)return;
  try{
    const stat=await fs.stat(result.filePaths[0]);
    if(stat.size>32*1024*1024)throw new Error('宠物包不能超过 32 MB。');
    const text=await fs.readFile(result.filePaths[0],'utf8'),value=await verify(text);
    await fs.mkdir(app.getPath('userData'),{recursive:true});
    await fs.writeFile(storage()+'.tmp',JSON.stringify(value),'utf8');
    await fs.rename(storage()+'.tmp',storage());
    savedText=JSON.stringify(value);autoPlay=value.settings.autoPlay;
    send('package',{text:savedText});recenter();showPet();refreshTray();
  }catch(error){dialog.showErrorBox('没有导入成功',error.message);}
}
function menu(){
  return Menu.buildFromTemplate([
    {label:'打开制作器',click:maker},{label:'导入宠物包…',click:importPet},
    {type:'separator'},
    {label:'自动活动',type:'checkbox',checked:autoPlay,click:async item=>{
      autoPlay=item.checked;send('auto',{enabled:autoPlay});refreshTray();
      if(savedText){const value=parsePackage(savedText);value.settings.autoPlay=autoPlay;savedText=JSON.stringify(value);await fs.writeFile(storage(),savedText);}
    }},
    {label:'走一走',click:()=>send('walk')},{label:'伸懒腰',click:()=>send('stretch')},
    {label:'睡一会儿',click:()=>send('sleep')},{label:'叫醒它',click:()=>send('wake')},
    {label:'暂停 / 继续',click:()=>send('pause')},{label:'开始 / 结束 25 分钟专注',click:()=>send('focus')},
    {type:'separator'},
    {label:'显示并回到屏幕内',click:()=>{recenter();showPet();}},
    {label:'隐藏猫咪',click:()=>petWindow.hide()},{label:'退出桌宠',click:()=>app.quit()},
  ]);
}
function refreshTray(){if(tray)tray.setContextMenu(menu());}
if(!app.requestSingleInstanceLock()){app.quit();}
else{
app.on('second-instance',()=>{if(petWindow){showPet();recenter();}});
app.whenReady().then(async()=>{
  ({parsePackage}=await import('../shared/pet-package.mjs'));
  ({clampPosition}=await import('../shared/engine.mjs'));
  session.defaultSession.setPermissionRequestHandler((_wc,_permission,callback)=>callback(false));
  session.defaultSession.webRequest.onHeadersReceived((details,callback)=>callback({responseHeaders:{...details.responseHeaders,'Content-Security-Policy':["default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self' data: blob:; object-src 'none'; base-uri 'none'"]}}));
  try{const text=await fs.readFile(storage(),'utf8');const value=await verify(text);savedText=JSON.stringify(value);autoPlay=value.settings.autoPlay;}catch(error){if(error.code!=='ENOENT'&&!smoke)dialog.showErrorBox('上次的宠物包没有读成功','将先显示示例猫咪。原文件保留在本机，可以重新导入。');}
  petWindow=new BrowserWindow({width:400,height:420,frame:false,transparent:true,resizable:false,hasShadow:false,alwaysOnTop:true,skipTaskbar:true,show:false,title:'Desktop Pets Beta',webPreferences:{...safePreferences,backgroundThrottling:false}});
  secure(petWindow);petWindow.setIgnoreMouseEvents(true,{forward:true});recenter();
  if(process.platform==='darwin'){petWindow.setVisibleOnAllWorkspaces(true,{visibleOnFullScreen:true});app.dock.hide();}
  if(!smoke){
    const icon=nativeImage.createFromPath(path.join(__dirname,'../dist/demo/tray.png'));
    tray=new Tray(icon.resize({width:20,height:20}));tray.setToolTip('桌宠工坊 · 右键管理');refreshTray();
    tray.on('double-click',()=>{showPet();recenter();});
    petWindow.once('ready-to-show',showPet);
  }
  let hit=false;
  ipcMain.handle('pet:load',event=>{if(!allowed(event))throw new Error('Invalid sender');return savedText;});
  ipcMain.handle('pet:save',async(event,text)=>{
    if(!allowed(event))throw new Error('Invalid sender');
    const value=await verify(text);
    const result=await dialog.showSaveDialog(studioWindow,{title:'保存宠物包',defaultPath:value.pet.name.replace(/[\\/:*?"<>|]/g,'_')+'.petpack.json',filters:[{name:'宠物包',extensions:['json']}]});
    if(result.canceled)return false;
    await fs.writeFile(result.filePath,JSON.stringify(value),'utf8');return true;
  });
  ipcMain.handle('pet:move',(event,dx)=>{
    if(!petOnly(event)||dragTimer||!petWindow.isVisible()||!Number.isFinite(dx)||Math.abs(dx)>15)return 0;
    const now=Date.now();if(now-lastMove<25)return 0;lastMove=now;
    const b=petWindow.getBounds(),area=screen.getDisplayMatching(b).workArea;
    const next=clampPosition(b.x+dx,b.y,b.width,b.height,area);
    petWindow.setPosition(next.x,next.y,false);
    return next.x===area.x?1:next.x===area.x+area.width-b.width?-1:0;
  });
  ipcMain.on('pet:hit',(event,value)=>{if(petOnly(event)&&!dragTimer&&hit!==value){hit=value;petWindow.setIgnoreMouseEvents(!value,{forward:true});}});
  ipcMain.on('pet:drag',(event,active)=>{
    if(!petOnly(event))return;
    clearInterval(dragTimer);dragTimer=null;
    if(active){
      const cursor=screen.getCursorScreenPoint(),bounds=petWindow.getBounds();
      dragOrigin={x:cursor.x-bounds.x,y:cursor.y-bounds.y};petWindow.setIgnoreMouseEvents(false);
      dragTimer=setInterval(()=>{
        const cursor=screen.getCursorScreenPoint(),b=petWindow.getBounds(),area=screen.getDisplayNearestPoint(cursor).workArea;
        const next=clampPosition(cursor.x-dragOrigin.x,cursor.y-dragOrigin.y,b.width,b.height,area);
        petWindow.setPosition(next.x,next.y,false);
      },16);
    }
  });
  ipcMain.on('pet:menu',event=>{if(petOnly(event))menu().popup({window:petWindow});});
  ipcMain.on('pet:notify',(event,message)=>{
    if(!petOnly(event)||typeof message!=='string'||message.length>80||Date.now()-lastNotify<30_000)return;
    lastNotify=Date.now();if(Notification.isSupported())new Notification({title:'桌宠的提醒',body:message}).show();
  });
  screen.on('display-removed',()=>recenter());
  await petWindow.loadFile(outIndex);
  if(smoke){
    showPet();
    await new Promise(resolve=>setTimeout(resolve,200));
    const output=path.resolve(process.env.PET_SMOKE_OUTPUT||'electron-smoke.json');
    // Native window setup and shared-parser checks; physical Mac interaction is a separate acceptance step.
    let photoRendered=false;
    for(let attempt=0;attempt<60;attempt++){
      photoRendered=await petWindow.webContents.executeJavaScript("(()=>{const c=document.querySelector('canvas');return !!c&&c.width>0&&Array.from(c.getContext('2d').getImageData(0,0,c.width,c.height).data).some((v,i)=>i%4===3&&v>24)})()");
      if(photoRendered)break;
      await new Promise(resolve=>setTimeout(resolve,100));
    }
    const canvas=await petWindow.webContents.capturePage();
    await fs.writeFile(output.replace(/\.json$/,'.png'),canvas.toPNG());
    const report={photoRendered,transparentCorner:canvas.toBitmap()[3]===0,platform:process.platform,arch:process.arch,loaded:!petWindow.webContents.isLoading(),transparentWindowRequested:true,alwaysOnTop:petWindow.isAlwaysOnTop(),sandbox:petWindow.webContents.getLastWebPreferences().sandbox,bounds:petWindow.getBounds()};
    await fs.writeFile(output,JSON.stringify(report,null,2));app.exit(Object.values(report).includes(false)?1:0);
  }
}).catch(error=>{console.error(error.message);app.exit(1);});
app.on('window-all-closed',()=>{if(smoke)app.quit();});
app.on('before-quit',()=>{clearInterval(dragTimer);tray?.destroy();});
}
