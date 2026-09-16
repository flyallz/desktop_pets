import React, {useEffect, useRef, useState} from 'react';
import {PetEngine} from '../shared/engine.mjs';
import {loadImage} from './images.mjs';
import {LOOKS} from '../shared/pet-package.mjs';
import {drawAccessory} from './accessories.mjs';
import {PetMenu, AccessoryFit} from './PetControls.jsx';

export const ACTION_NAMES = {idle:'原地陪伴',walk:'走一走',stretch:'伸懒腰',sleep:'睡一会儿'};
export default function PetStage({pet, command, onState, onAppearanceChange, background='mist', desktop=false}) {
  const canvasRef = useRef(null);
  const engine = useRef(null);
  const images = useRef(new Map());
  const onStateRef = useRef(onState);
  const pointer = useRef(null);
  const stageRef = useRef(null);
  const [menuOpen,setMenuOpen] = useState(false);
  const [draft,setDraft] = useState(null);
  const [saving,setSaving] = useState(false);
  const draftRef = useRef(null), controlsRef = useRef(false);
  const controlsOpen = menuOpen || !!draft;
  draftRef.current=draft; controlsRef.current=controlsOpen;
  function closeControls(restoreFocus=false) {
    if(saving)return;
    setMenuOpen(false);setDraft(null);
    if(restoreFocus)canvasRef.current?.focus({preventScroll:true});
  }
  useEffect(()=>{
    if(!controlsOpen)return;
    const dismiss=event=>{
      if(event.type==='keydown'){
        if(event.key!=='Escape')return;
        event.preventDefault();closeControls(true);
      }else if(event.type==='blur')closeControls();
      else if(!stageRef.current?.contains(event.target))closeControls();
    };
    document.addEventListener('pointerdown',dismiss);
    document.addEventListener('keydown',dismiss);
    window.addEventListener('blur',dismiss);
    return()=>{document.removeEventListener('pointerdown',dismiss);document.removeEventListener('keydown',dismiss);window.removeEventListener('blur',dismiss);};
  },[controlsOpen,saving]);
  useEffect(()=>{
    if(desktop)window.petDesktop?.hit(controlsOpen);
  },[controlsOpen,desktop]);
  const [bubble, setBubble] = useState('');
  const [loadError, setLoadError] = useState('');
  const bubbleTimer = useRef(null);
  const position = useRef({x:0,y:0});
  const speak = message => {
    setBubble(message);
    clearTimeout(bubbleTimer.current);
    bubbleTimer.current = setTimeout(() => setBubble(''), 5000);
  };
  onStateRef.current = onState;
  useEffect(() => {
    if (!engine.current) {engine.current = new PetEngine(pet);engine.current.paused = window.matchMedia('(prefers-reduced-motion: reduce)').matches;}
    else engine.current.setPet(pet);
  }, [pet]);
  useEffect(() => {
    let cancelled = false;
    setLoadError('');
    Promise.all(pet.assets.map(async asset => [asset.id, await loadImage(asset.data)])).then(entries => {
      if (!cancelled) images.current = new Map(entries);
    }).catch(() => { if (!cancelled) setLoadError('有图片无法显示，请重新导入这张图片。'); });
    return () => {cancelled=true;};
  }, [pet.assets]);
  useEffect(() => {
    if (!command || !engine.current) return;
    const value = engine.current;
    closeControls();
    if (command.action === 'pause') value.paused = !value.paused;
    else if (command.action === 'focus') {
      value.focusRemaining = value.focusRemaining === null ? 25 * 60 : null;
      speak(value.focusRemaining === null ? '已结束这次专注。' : '接下来 25 分钟，我在这里陪你。');
    } else if (command.action === 'wake') value.wake();
    else if (command.action === 'greet') speak('我在这里，慢慢来。');
    else if (command.action === 'home') { value.x = 0; position.current={x:0,y:0}; }
    else value.play(command.action);
  }, [command]);
  useEffect(() => {
    let raf, previous = performance.now(), lastNotify = 0, movementClock=0, pendingDx=0;
    const canvas = canvasRef.current;
    const context = canvas.getContext('2d', {willReadFrequently:desktop});
    let w=1,h=1;
    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      w=rect.width; h=rect.height;
      const ratio = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.round(w * ratio); canvas.height = Math.round(h * ratio);
      context.setTransform(ratio, 0, 0, ratio, 0, 0);
    };
    const observer = new ResizeObserver(resize);
    observer.observe(canvas);
    const frame = now => {
      const delta = (now - previous) / 1000;
      previous = now;
      const value = engine.current;
      if (value) {
        const petSize = Math.min(value.pet.settings.size * (desktop ? 1 : 1.36), h - 65, w * (desktop ? 0.97 : 0.68));
        const room = Math.max(0, w - petSize - 32);
        const state = value.tick(delta, desktop ? 0 : room, !!pointer.current || controlsRef.current);
        if (desktop && !pointer.current && !controlsRef.current && !state.paused && state.dx) {
          pendingDx += state.dx; movementClock += delta;
          if (movementClock > .045) {
            window.petDesktop?.move(pendingDx).then(direction => {if(direction) value.direction=direction;});
            pendingDx=0; movementClock=0;
          }
        } else {pendingDx=0;movementClock=0;}
        context.clearRect(0, 0, w, h);
        const image = images.current.get(state.assetId);
        if (image) {
          const cx = w / 2 + (desktop ? 0 : state.x) + position.current.x;
          const cy = h - 28 + position.current.y;
          context.save();
          context.translate(cx, cy);
          context.scale(state.action === 'walk' && state.direction === 1 ? -1 : 1, 1 + state.breath);
          context.drawImage(image, -petSize/2, -petSize, petSize, petSize);
          drawAccessory(context, draftRef.current || value.pet.settings.appearance, state.action, petSize);
          context.restore();
        }
        for (const message of state.events) {
          speak(message);
          if (desktop) window.petDesktop?.notify(message);
        }
        if (now - lastNotify > 200) {
          onStateRef.current?.({...state});
          lastNotify=now;
        }
      }
      raf=requestAnimationFrame(frame);
    };
    raf=requestAnimationFrame(frame);
    return () => {cancelAnimationFrame(raf);observer.disconnect();clearTimeout(bubbleTimer.current);};
  }, [desktop]);
  function point(event) {
    const box=canvasRef.current.getBoundingClientRect();
    return {x:event.clientX-box.left,y:event.clientY-box.top};
  }
  function hits(event) {
    const c=canvasRef.current,p=point(event),box=c.getBoundingClientRect();
    try {
      const x=Math.max(0,Math.min(c.width-1,Math.floor(p.x*c.width/box.width)));
      const y=Math.max(0,Math.min(c.height-1,Math.floor(p.y*c.height/box.height)));
      return c.getContext('2d').getImageData(x,y,1,1).data[3] > 24;
    } catch {return false;}
  }
  function down(event) {
    if (event.button !== 0 || draftRef.current) return;
    if(!hits(event)){closeControls();return;}
    pointer.current={startX:event.clientX,startY:event.clientY,offset:{...position.current},moved:false,wasOpen:menuOpen};
    event.currentTarget.setPointerCapture(event.pointerId);
  }
  function move(event) {
    if (desktop) window.petDesktop?.hit(controlsRef.current || !!pointer.current || hits(event));
    if (!pointer.current) return;
    const dx=event.clientX-pointer.current.startX,dy=event.clientY-pointer.current.startY;
    if (!pointer.current.moved && Math.abs(dx)+Math.abs(dy)>5) {
      pointer.current.moved=true;setMenuOpen(false);
      if(desktop)window.petDesktop?.drag(true);
    }
    if (!desktop && pointer.current.moved) {
      const c=canvasRef.current.getBoundingClientRect();
      position.current={
        x:Math.max(-c.width*.25,Math.min(c.width*.25,pointer.current.offset.x+dx)),
        y:Math.max(-c.height*.25,Math.min(0,pointer.current.offset.y+dy)),
      };
    }
  }
  function up(event) {
    if (!pointer.current) return;
    const tap=!pointer.current.moved && event.type!=='pointercancel';
    const wasOpen=pointer.current.wasOpen;
    pointer.current=null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    if (desktop) window.petDesktop?.drag(false);
    if(tap){setBubble('');setMenuOpen(!wasOpen);}
  }
  function chooseAction(action) {
    if(action==='cycle')engine.current.cycle();else engine.current.play(action);
    setBubble('');closeControls(true);
  }
  async function applyAppearance(appearance) {
    if(saving)return;
    setSaving(true);
    try {
      await onAppearanceChange(appearance);
      setMenuOpen(false);setDraft(null);setBubble('');
      canvasRef.current?.focus({preventScroll:true});
    }catch(error){speak(error.message || '造型没有保存，请重试。');}
    finally{setSaving(false);}
  }
  const action=engine.current?.action || 'idle';
  return <div ref={stageRef} className={'pet-stage stage-'+background+(desktop?' desktop-stage':'')}
    onPointerDown={event=>{if(controlsOpen && event.target===stageRef.current)closeControls();}}>
    {bubble && <div className="pet-bubble" role="status">{bubble}</div>}
    {loadError && <p className="stage-error" role="alert">{loadError}</p>}
    <canvas ref={canvasRef} aria-label={pet.pet.name+'的动作预览，点击选择动作和造型，按住拖动'} role="button" tabIndex="0" aria-haspopup="dialog" aria-expanded={controlsOpen}
      onKeyDown={event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();if(!draft)setMenuOpen(value=>!value);}}}
      onPointerDown={down} onPointerMove={move} onPointerUp={up} onPointerCancel={up}
      onPointerLeave={() => {if(desktop && !pointer.current && !controlsRef.current) window.petDesktop?.hit(false);}}
      onContextMenu={event => {if(desktop){event.preventDefault();closeControls();window.petDesktop?.menu();}}}/>
    {menuOpen&&<PetMenu action={action} available={Object.keys(pet.actions)} appearance={pet.settings.appearance} busy={saving}
      onAction={chooseAction} onLook={style=>applyAppearance({...pet.settings.appearance,style})}
      onCycleLook={()=>applyAppearance({...pet.settings.appearance,style:LOOKS[(LOOKS.indexOf(pet.settings.appearance.style)+1)%LOOKS.length]})}
      onAdjust={()=>{setDraft(structuredClone(pet.settings.appearance));setMenuOpen(false);}}
      onClose={()=>closeControls(true)}/>}
    {draft&&<AccessoryFit appearance={draft} action={action} busy={saving} onChange={setDraft} onSave={()=>applyAppearance(draft)} onCancel={()=>closeControls(true)}/>}
  </div>;
}
