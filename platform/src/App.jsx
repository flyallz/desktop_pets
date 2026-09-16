import React, {useEffect, useRef, useState} from 'react';
import {parsePackage, validatePackage, serializePackage, newPackage, pruneAssets, MAX_PACKAGE_BYTES} from '../shared/pet-package.mjs';
import {imageToAsset, loadDemo, saveDownload} from './images.mjs';
import PetStage, {ACTION_NAMES} from './PetStage.jsx';
import PhotoEditor from './PhotoEditor.jsx';

const icons = {
 cat:<><path d="M4 17V5l6 4h4l6-4v12c0 3-16 3-16 0Z"/><path d="M8 13h.01M16 13h.01M11 16l1 1 1-1"/></>,
 upload:<path d="M12 16V3m-4 4 4-4 4 4M4 16v5h16v-5"/>,
 save:<path d="M12 3v13m-4-4 4 4 4-4M4 17v4h16v-4"/>,
 play:<path d="m8 4 12 8-12 8Z"/>,pause:<path d="M8 4v16M16 4v16"/>,
 image:<><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="8" r="1.5"/><path d="m3 17 5-5 4 4 4-7 5 6"/></>,
 arrow:<path d="M4 12h16m-6-6 6 6-6 6"/>,close:<path d="m5 5 14 14M5 19 19 5"/>,
};
function Icon({name}) {return <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{icons[name]}</svg>;}
const STEPS=['照片','动作','陪伴'];
function getFrame(pet,id) {return pet?.assets.find(asset=>asset.id===pet.actions[id]?.frames[0])?.data;}
export default function App() {
  const desktop = !!window.petDesktop && window.location.hash !== '#studio';
  const [pet,setPet]=useState(null),[isDemo,setIsDemo]=useState(true);
  const [step,setStep]=useState(0),[background,setBackground]=useState('mist');
  const [command,setCommand]=useState(null),[state,setState]=useState({action:'idle',paused:false,focusRemaining:null});
  const [notice,setNotice]=useState(''),[error,setError]=useState(''),[busy,setBusy]=useState('');
  const [dirty,setDirty]=useState(false);
  const [ready,setReady]=useState(false),[editor,setEditor]=useState(false),[exported,setExported]=useState(false);
  const [poseAction,setPoseAction]=useState('walk'),[replacePending,setReplacePending]=useState(false);
  const photoInput=useRef(null),packInput=useRef(null),poseInput=useRef(null),noticeTimer=useRef(null);
  const send=action=>setCommand({action,id:Date.now()+Math.random()});
  const notify=message=>{setNotice(message);clearTimeout(noticeTimer.current);noticeTimer.current=setTimeout(()=>setNotice(''),6500);};
  const failure=e=>setError(e.message || '操作没有完成，请重试。');
  useEffect(()=>{
    let cancelled=false;
    async function start() {
      try {
        const stored=window.petDesktop ? await window.petDesktop.load() : null;
        const value=stored ? parsePackage(stored) : validatePackage(await loadDemo());
        if(!cancelled){setPet(value);setIsDemo(!stored);}
      } catch(e){if(!cancelled) failure(e);}
    }
    start();
    if(!desktop)fetch('/api/health').then(r=>r.ok?r.json():null).then(data=>{if(!cancelled)setReady(!!data?.ready && data?.mode==='local');}).catch(()=>{});
    const detach=window.petDesktop?.onCommand(event=>{
      if(event.action==='package'){setPet(parsePackage(event.text));setIsDemo(false);setCommand({action:'home',id:Date.now()});}
      else if(event.action==='auto') setPet(value=>({...value,settings:{...value.settings,autoPlay:event.enabled}}));
      else setCommand({action:event.action,id:Date.now()+Math.random()});
    });
    return ()=>{cancelled=true;detach?.();clearTimeout(noticeTimer.current);};
  }, [desktop]);
  useEffect(()=>{
    const warn=event=>{if(dirty){event.preventDefault();event.returnValue='';}};
    window.addEventListener('beforeunload',warn);
    return()=>window.removeEventListener('beforeunload',warn);
  },[dirty]);
  useEffect(()=>{
    if(step>0 && window.matchMedia('(max-width: 760px)').matches) document.querySelector('.step-content')?.scrollIntoView({block:'start'});
  },[step]);
  function changePet(next) {setDirty(true);setPet(next);setExported(false);setError('');}
  async function changeAppearance(appearance) {
    if(desktop){
      const saved=await window.petDesktop.appearance(appearance);
      setPet(current=>({...current,settings:{...current.settings,appearance:saved}}));
    }else changePet({...pet,settings:{...pet.settings,appearance}});
  }
  function setting(key,value) {
    changePet({...pet,settings:{...pet.settings,[key]:value}});
    if(key==='autoPlay' && !value)send('idle');
  }
  async function importFile(file) {
    if(!file)return;
    setBusy('正在读取宠物包');setError('');
    try {
      if(file.size>MAX_PACKAGE_BYTES)throw new Error('宠物包需小于 32 MB。');
      const next=parsePackage(await file.text());
      for(const asset of next.assets){const image=new Image();image.src=asset.data;await image.decode();}
      changePet(next);setIsDemo(false);setStep(0);send('home');notify('宠物包已打开，照片和设置都在。');
    }catch(e){failure(e);}finally{setBusy('');}
  }
  async function usePhoto(file) {
    if(!file)return;
    setBusy('正在读取照片');setError('');
    try {
      const asset=await imageToAsset(file);
      changePet(newPackage(isDemo?'我的宠物':pet.pet.name,asset));
      setIsDemo(false);setStep(0);send('home');notify('已换成你的照片。其他姿态可在“动作”中补充。');
    }catch(e){failure(e);}finally{setBusy('');}
  }
  async function cutout() {
    setBusy('正在本机抠图，通常需要十几秒');setError('');
    try {
      const blob=await (await fetch(getFrame(pet,'idle'))).blob();
      const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),65_000);
      let response;
      try {response=await fetch('/api/cutout',{method:'POST',headers:{'Content-Type':'image/png'},body:blob,signal:controller.signal});}
      finally{clearTimeout(timer);}
      if(!response.ok)throw new Error((await response.json()).error || '抠图暂时不可用。');
      const asset=await imageToAsset(await response.blob(),pet.actions.idle.frames[0]);
      changePet({...pet,assets:pet.assets.map(old=>old.id===asset.id?asset:old)});
      notify('背景已去除，可以继续用画笔修整边缘。');
    }catch(e){failure(e.name==='AbortError'?new Error('处理超时，请换一张照片重试。'):e);}finally{setBusy('');}
  }
  async function addPose(files) {
    const list=Array.from(files || []);
    if(!list.length)return;
    setBusy('正在整理动作图片');setError('');
    try {
      if(list.length>24)throw new Error('一个动作最多支持 24 帧。');
      if(poseAction==='walk' && list.length<4)throw new Error('行走请选择至少 4 张按顺序命名的透明图片。');
      list.sort((a,b)=>a.name.localeCompare(b.name,'zh-CN',{numeric:true}));
      const assets=[];
      for(let i=0;i<list.length;i++)assets.push(await imageToAsset(list[i],poseAction+'-'+i));
      const next=pruneAssets({...pet,assets:[...pet.assets.filter(a=>!a.id.startsWith(poseAction+'-')), ...assets],
        actions:{...pet.actions,[poseAction]:{frames:assets.map(a=>a.id),fps:poseAction==='walk'?8:Math.min(8,assets.length)}}});
      changePet(validatePackage(next));send(poseAction);notify(ACTION_NAMES[poseAction]+'已添加，按文件名顺序播放。');
    }catch(e){failure(e);}finally{setBusy('');}
  }
  async function exportPet() {
    setError('');
    try {
      const text=serializePackage(pet);
      const filename=(pet.pet.name.replace(/[\\/:*?"<>|]/g,'_')||'我的宠物')+'.petpack.json';
      if(window.petDesktop){const saved=await window.petDesktop.save(text);if(!saved)return;}
      else saveDownload(text,filename);
      setDirty(false);setExported(true);notify('宠物包已准备好。请保管下载的文件，之后可以重新导入。');
    }catch(e){failure(e);}
  }
  async function restoreDemo() {
    setBusy('正在载入示例');setError('');
    try{changePet(validatePackage(await loadDemo()));setIsDemo(true);setReplacePending(false);send('home');notify('已恢复示例猫咪。');}
    catch(e){failure(e);}finally{setBusy('');}
  }
  if(!pet)return <main className="loading-screen"><Icon name="cat"/><h1>桌宠工坊</h1><p role={error?'alert':'status'}>{error || '正在准备猫咪的照片和动作…'}</p>{error&&<button onClick={()=>location.reload()}>重新加载</button>}</main>;
  if(desktop)return <main className="desktop-shell"><PetStage pet={pet} command={command} onState={setState} onAppearanceChange={changeAppearance} desktop/></main>;
  const minutes=state.focusRemaining===null?null:Math.ceil(state.focusRemaining/60);
  return <div className="app-shell">
    <header className="site-header">
      <a href="./" className="brand" aria-label="桌宠工坊首页"><span className="brand-mark"><Icon name="cat"/></span><span>桌宠工坊<span className="beta-label">内测版</span></span></a>
      <div className="header-actions"><button className="quiet import-button" onClick={()=>packInput.current.click()} disabled={!!busy}>打开宠物包</button><button className="primary" onClick={exportPet} disabled={!!busy}><Icon name="save"/>保存宠物包</button></div>
    </header>
    <main>
      <div className="page-heading"><div><h1>把它带到你的桌面。</h1><p>用自己的照片，做一只陪你上班的桌宠。</p></div><a href="#take-home">如何带到桌面 <Icon name="arrow"/></a></div>
      <section className="maker" aria-label="桌宠制作器">
        <aside className="tool-rail">
          <nav className="steps" aria-label="制作步骤">{STEPS.map((title,i)=><button key={title} onClick={()=>setStep(i)} aria-current={step===i?'step':undefined}><span className="step-number">{i+1}</span><strong>{title}</strong></button>)}</nav>
          <div className="step-content" key={step}>
          {step===0&&<>
            <h2>先选一张喜欢的照片</h2><p className="helper">全身清楚、背景简单的照片，效果会更好。</p>
            <button className="photo-upload" onClick={()=>photoInput.current.click()} disabled={!!busy}
              onDragOver={e=>e.preventDefault()} onDrop={e=>{e.preventDefault();if(!busy)usePhoto(e.dataTransfer.files[0]);}}>
              <Icon name="upload"/><strong>选择宠物照片</strong><span>也可以拖到这里</span><small>JPG / PNG / WebP · 10 MB 以内</small>
            </button>
            <label className="field">它叫什么？<input value={pet.pet.name} maxLength={30} onChange={e=>changePet({...pet,pet:{name:e.target.value}})} placeholder="给桌宠起个名字"/></label>
            <div className="photo-tools"><button onClick={cutout} disabled={!!busy||!ready} title={ready?'只在本机处理':'需启动本机抠图工具；也可导入透明 PNG'}>自动去背景</button><button onClick={()=>setEditor(true)} disabled={!!busy}>画笔修整</button></div>
            <p className="fine-print">{ready?'本机抠图已就绪，照片不会上传到云端。':'可导入透明 PNG，或用画笔擦除背景。本机抠图需另外启动。'}</p>
            <button className="text-link next-step" onClick={()=>setStep(1)}>接着设置动作 <Icon name="arrow"/></button>
          </>}
          {step===1&&<>
            <h2>让它动起来</h2><p className="helper">点下方照片试播。自动活动会在已有动作间轮换。</p>
            <label className="toggle-row"><span>自行切换动作<small>走走、歇歇，再睡一会儿</small></span><input type="checkbox" role="switch" checked={pet.settings.autoPlay} onChange={e=>setting('autoPlay',e.target.checked)}/></label>
            <label className="field">活动节奏<select value={pet.settings.activity} onChange={e=>setting('activity',e.target.value)}><option value="calm">安静一点</option><option value="lively">活泼一点</option></select></label>
            <div className="divider"/><h3>补充自己的姿态</h3><p className="helper">上传同一只宠物的透明图片。走路需至少 4 帧，文件名决定播放顺序。</p>
            <label className="field">动作<select value={poseAction} onChange={e=>setPoseAction(e.target.value)}><option value="walk">走一走（4～24 帧）</option><option value="stretch">伸懒腰</option><option value="sleep">睡一会儿</option></select></label>
            <button className="full-width" disabled={!!busy} onClick={()=>poseInput.current.click()}><Icon name="image"/>添加动作图片</button>
            {pet.actions[poseAction]&&<button className="text-link remove-action" onClick={()=>{const actions={...pet.actions};delete actions[poseAction];changePet(pruneAssets({...pet,actions}));send('idle');}}>移除这个动作</button>}
            <p className="fine-print">照片生成新姿态还在准备中。自己的照片不会套用示例猫的动作。</p>
            <button className="text-link next-step" onClick={()=>setStep(2)}>再调一下陪伴方式 <Icon name="arrow"/></button>
          </>}
          {step===2&&<>
            <h2>按你的节奏陪伴</h2><p className="helper">这些设置会跟着宠物包一起保存。</p>
            <label className="field">桌宠大小 <span className="value-label">{pet.settings.size} px</span><input type="range" min="160" max="360" step="10" value={pet.settings.size} onChange={e=>setting('size',Number(e.target.value))}/></label>
            <label className="toggle-row"><span>休息提醒<small>忙起来，也记得照顾自己</small></span><input type="checkbox" role="switch" checked={pet.settings.reminder.enabled} onChange={e=>setting('reminder',{...pet.settings.reminder,enabled:e.target.checked})}/></label>
            {pet.settings.reminder.enabled&&<><label className="field">每隔多久<select value={pet.settings.reminder.minutes} onChange={e=>setting('reminder',{...pet.settings.reminder,minutes:Number(e.target.value)})}>{[15,30,45,60,90].map(n=><option key={n} value={n}>{n} 分钟</option>)}</select></label><label className="field">提醒时说一句<input maxLength={80} value={pet.settings.reminder.message} onChange={e=>setting('reminder',{...pet.settings.reminder,message:e.target.value})}/></label></>}
            <div className="divider"/><h3>试一次专注陪伴</h3><p className="helper">陪你安静工作 25 分钟，结束时轻声提醒。</p>
            <button className="full-width" onClick={()=>send('focus')}>{minutes===null?'开始 25 分钟专注':'结束专注 · 剩余 '+minutes+' 分钟'}</button>
            <p className="fine-print">网页需保持打开。安装客户端后，可在桌面上使用。</p>
            <button className="primary full-width save-side" onClick={exportPet} disabled={!!busy}>保存我的桌宠</button>
          </>}
          </div><div className="rail-foot"><span>照片与配置仅在当前页面中保留。</span><span>关掉前，记得保存宠物包。</span></div>
        </aside>
        <div className="work-surface">
          <div className="stage-heading"><div><span className="pet-name">{pet.pet.name||'我的宠物'}</span><span className="demo-tag">{isDemo?'示例猫咪':'我的桌宠'}</span></div>
            <div className="background-picker" aria-label="预览背景">{[['mist','浅灰'],['sage','浅绿'],['night','深色']].map(([id,label])=><button key={id} className={'swatch swatch-'+id} aria-label={label+'背景'} aria-pressed={background===id} onClick={()=>setBackground(id)}/>)}</div>
          </div>
          <button className="mobile-upload" disabled={!!busy} onClick={()=>photoInput.current.click()}><Icon name="upload"/>换成我的照片</button>
          {editor?<PhotoEditor asset={pet.assets.find(a=>a.id===pet.actions.idle.frames[0])} onCancel={()=>setEditor(false)} onSave={asset=>{changePet({...pet,assets:pet.assets.map(a=>a.id===asset.id?asset:a)});setEditor(false);notify('已保存背景修整。');}}/>
            :<PetStage pet={pet} command={command} onState={setState} onAppearanceChange={changeAppearance} background={background}/>}
          <div className="playback-bar"><div className="live-status"><span className={state.paused?'status-dot paused':'status-dot'}/>{state.paused?'已暂停':ACTION_NAMES[state.action]}<span className="status-detail">{pet.settings.autoPlay?'自动活动已开':'手动试播'}</span></div>
            <div><button className="icon-button" onClick={()=>send('home')} aria-label="宠物回到中央" title="回到中央"><Icon name="image"/></button><button className="quiet pause-button" onClick={()=>send('pause')}><Icon name={state.paused?'play':'pause'}/>{state.paused?'继续':'暂停'}</button></div></div>
          <div className="action-strip" aria-label="动作试播">{Object.entries(ACTION_NAMES).map(([id,label])=>{
              const available=!!pet.actions[id];
              return <button key={id} className={'action-tile'+(state.action===id?' selected':'')} aria-pressed={state.action===id} onClick={()=>available?send(id):(setStep(1),setPoseAction(id),notify('先添加这个动作的图片，再来试播。'))}>
                <div className="action-photo">{available?<img src={getFrame(pet,id)} alt="" draggable="false"/>:<Icon name="image"/>}{state.action===id&&<span className="playing-mark"><Icon name="play"/></span>}</div>
                <span className="action-label">{label}<small>{available?(id==='walk'?pet.actions[id].frames.length+' 帧动作':id==='idle'?'轻轻呼吸':id==='sleep'?'安静地睡':'舒展一下'):'待添加图片'}</small></span>
              </button>;
            })}</div>
          <div className="surface-caption"><span>点击选动作和造型，拖动换个位置。<span className="desktop-only"> 预览背景不会进入宠物包。</span></span><button className="text-link" onClick={()=>isDemo?send('greet'):setReplacePending(true)}>{isDemo?'打个招呼':'重看示例'}</button></div>
          {replacePending&&<div className="inline-confirm"><p>恢复示例会替换当前作品，请先保存宠物包。</p><button onClick={()=>setReplacePending(false)}>保留当前作品</button><button onClick={restoreDemo}>恢复示例</button></div>}
        </div>
      </section>
      <section id="take-home" className="take-home">
        <div><h2>{exported?'宠物包已准备好，下一站是桌面。':'做好以后，把陪伴带走。'}</h2><p>一份宠物包，保留照片、动作和你的设置。Windows、Mac 使用同一份文件。</p></div>
        <ol><li><strong>保存宠物包</strong><span>文件留在自己手里，随时重新编辑。</span></li><li><strong>安装桌面客户端</strong><span>Windows 与 Mac M / Intel 共用这一套动作。</span></li><li><strong>导入，开始陪伴</strong><span>在客户端右键猫咪，选择“导入宠物包”。</span></li></ol>
        <div className="download-row"><p>已提供 Windows 与 Mac 内测包。Mac 支持 M / Intel，需 macOS 13+，尚未签名。</p><a className="text-link" href="https://github.com/flyallz/desktop_pets/releases" target="_blank" rel="noreferrer">下载内测客户端 <Icon name="arrow"/></a></div>
      </section>
    </main>
    <footer className="site-footer"><span>桌宠工坊 · 从一张自己的照片开始</span><span>本地制作内测 · 暂未接入付费和在线生成</span></footer>
    <input ref={photoInput} type="file" accept="image/png,image/jpeg,image/webp" className="sr-only" tabIndex="-1" onChange={e=>{usePhoto(e.target.files[0]);e.target.value='';}}/>
    <input ref={packInput} type="file" accept=".json,.petpack" className="sr-only" tabIndex="-1" onChange={e=>{importFile(e.target.files[0]);e.target.value='';}}/>
    <input ref={poseInput} type="file" multiple accept="image/png,image/webp" className="sr-only" tabIndex="-1" onChange={e=>{addPose(e.target.files);e.target.value='';}}/>
    {busy&&<div className="progress-notice" role="status"><span className="busy-dot"/>{busy}…</div>}
    {notice&&!busy&&<div className="toast" role="status">{notice}<button aria-label="关闭提示" onClick={()=>setNotice('')}><Icon name="close"/></button></div>}
    {error&&<div className="error-notice" role="alert"><span>{error}</span><button aria-label="关闭错误提示" onClick={()=>setError('')}><Icon name="close"/></button></div>}
  </div>;
}
